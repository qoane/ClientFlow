using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClientFlow.Application.Abstractions;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Feedback;
using ClientFlow.Domain.Surveys;
using ClientFlow.Infrastructure;
using ClientFlow.Web.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveysController(
    SurveyService svc,
    ISurveyRepository surveys,
    IResponseRepository responses,
    IUnitOfWork uow,
    AppDbContext db) : ControllerBase
{
    private const string DefaultKioskSurveyCode = "liberty-nps";
    private static readonly JsonSerializerOptions DefinitionJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> DisplayOnlyQuestionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "message",
        "static_text",
        "static_html",
        "static-html",
        "image",
        "video",
        "divider",
        "spacer"
    };

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
        => (await svc.GetByCodeAsync(code, ct)) is { } s ? Ok(s) : NotFound();

    [HttpPost("{code}/responses")]
    public async Task<IActionResult> Submit(string code, [FromBody] SubmitResponseDto dto, CancellationToken ct)
        => (await svc.SubmitAsync(code, dto, ct)) is { } id ? Ok(new { id }) : NotFound();

    public sealed record SubmitSurveyRequest(
        [property: JsonPropertyName("answers")] Dictionary<string, string?>? Answers,
        [property: JsonPropertyName("additionalData")] SubmitSurveyAdditionalData? AdditionalData);

    public sealed record SubmitSurveyAdditionalData(
        [property: JsonPropertyName("clientCode")] string? ClientCode,
        [property: JsonPropertyName("formKey")] string? FormKey,
        [property: JsonPropertyName("libertyAnswers")] Dictionary<string, string?>? LibertyAnswers);

    [HttpPost("{code}/submit")]
    public async Task<IActionResult> SubmitSurvey(string code, [FromBody] SubmitSurveyRequest req, CancellationToken ct)
    {
        var survey = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (survey is null)
        {
            return NotFound(new { message = "Survey not found or inactive." });
        }

        var answers = MergeAnswers(req.Answers, req.AdditionalData?.LibertyAnswers);

        if (string.Equals(survey.Code, DefaultKioskSurveyCode, StringComparison.OrdinalIgnoreCase))
        {
            var missingKiosk = RequiredKioskKeys
                .Where(key => !answers.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (missingKiosk.Length > 0)
            {
                return BadRequest(new { message = "Missing required kiosk answers.", missing = missingKiosk });
            }

            var phoneValue = answers.TryGetValue("phone", out var phoneRaw)
                ? NormalizePhone(phoneRaw)
                : null;
            if (!IsValidLocalPhone(phoneValue))
            {
                return BadRequest(new { message = "Please enter an 8 digit local phone number." });
            }
        }

        var missing = survey.Questions
            .Where(q => q.Required)
            .Where(q => !DisplayOnlyQuestionTypes.Contains(q.Type ?? string.Empty))
            .Where(q => !answers.TryGetValue(q.Key, out var value) || string.IsNullOrWhiteSpace(value))
            .Select(q => q.Key)
            .ToArray();

        if (missing.Length > 0)
        {
            return BadRequest(new { message = "Missing required answers.", missing });
        }

        var questionsByKey = survey.Questions.ToDictionary(q => q.Key, StringComparer.OrdinalIgnoreCase);

        var response = new Response
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            Channel = "web",
            StartedUtc = TryGetDateTimeOffset(answers, "__startedUtc"),
            DurationSeconds = TryGetInt(answers, "__durationSeconds", out var duration) ? duration : null,
            ClientCode = ResolveMetaValue(req.AdditionalData?.ClientCode, answers, "__clientCode"),
            FormKey = ResolveMetaValue(req.AdditionalData?.FormKey, answers, "__formKey")
        };

        foreach (var entry in answers)
        {
            if (!questionsByKey.TryGetValue(entry.Key, out var question))
            {
                continue;
            }

            if (DisplayOnlyQuestionTypes.Contains(question.Type ?? string.Empty))
            {
                continue;
            }

            var answer = new Answer
            {
                Id = Guid.NewGuid(),
                Response = response,
                QuestionId = question.Id
            };

            if (IsNumericQuestion(question.Type))
            {
                if (decimal.TryParse(entry.Value, out var numericValue))
                {
                    answer.ValueNumber = numericValue;
                }
                else
                {
                    answer.ValueText = entry.Value;
                }
            }
            else
            {
                answer.ValueText = entry.Value;
            }

            response.Answers.Add(answer);
        }

        await responses.AddAsync(response, ct);
        await uow.SaveChangesAsync(ct);

        await TryRecordKioskFeedbackAsync(survey, answers, ct);

        return Ok(new { id = response.Id });
    }

    private async Task TryRecordKioskFeedbackAsync(Survey survey, Dictionary<string, string?> answers, CancellationToken ct)
    {
        try
        {
            if (!string.Equals(survey.Code, DefaultKioskSurveyCode, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!TryGetInt(answers, "satisfaction", out var overall) ||
                !TryGetInt(answers, "timeliness", out var time) ||
                !TryGetInt(answers, "professionalism", out var respect))
            {
                return;
            }

            var staff = await ResolveStaffAsync(answers, ct);
            if (staff is null)
            {
                return;
            }

            var branch = await ResolveBranchAsync(staff.BranchId, answers, ct);
            if (branch is null)
            {
                return;
            }

            var (branchId, branchName) = branch.Value;
            if (staff.BranchId is null)
            {
                staff.BranchId = branchId;
            }

            var durationSeconds = TryGetInt(answers, "__durationSeconds", out var duration) ? duration : 0;
            var startedUtc = TryGetDateTimeOffset(answers, "__startedUtc") ?? DateTimeOffset.UtcNow;
            var phone = answers.TryGetValue("phone", out var phoneRaw) ? NormalizePhone(phoneRaw) : null;

            var entity = new KioskFeedback
            {
                Id = Guid.NewGuid(),
                StaffId = staff.Id,
                TimeRating = time,
                RespectRating = respect,
                OverallRating = overall,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                StartedUtc = startedUtc,
                DurationSeconds = durationSeconds,
                BranchId = branchId,
                BranchName = branchName,
                CreatedUtc = DateTimeOffset.UtcNow
            };

            db.KioskFeedback.Add(entity);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Ignore kiosk mapping failures to avoid interrupting the main survey submission flow.
        }
    }

    [HttpGet("{code}/analytics")]
    public async Task<IActionResult> Analytics(
        string code,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var survey = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (survey is null)
        {
            return NotFound();
        }

        var questionIds = survey.Questions.Select(q => q.Id).ToList();

        var responsesQuery = db.Responses
            .AsNoTracking()
            .Include(r => r.Answers)
            .Where(r => r.SurveyId == survey.Id);

        if (from is not null)
        {
            responsesQuery = responsesQuery.Where(r => r.CreatedUtc >= from);
        }

        if (to is not null)
        {
            responsesQuery = responsesQuery.Where(r => r.CreatedUtc <= to);
        }

        var responses = await responsesQuery.ToListAsync(ct);

        var options = await db.Options
            .AsNoTracking()
            .Where(o => questionIds.Contains(o.QuestionId))
            .ToListAsync(ct);

        var analytics = SurveyAnalyticsBuilder.Build(survey, options, responses);
        return Ok(analytics);
    }

    private async Task<Staff?> ResolveStaffAsync(IReadOnlyDictionary<string, string?> answers, CancellationToken ct)
    {
        if (!answers.TryGetValue("staff", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var value = raw.Trim();
        if (Guid.TryParse(value, out var staffId))
        {
            return await db.Staff.FirstOrDefaultAsync(s => s.Id == staffId, ct);
        }

        var existing = await db.Staff.FirstOrDefaultAsync(s => s.Name == value, ct);
        if (existing is not null)
        {
            return existing;
        }

        var staff = new Staff
        {
            Id = Guid.NewGuid(),
            Name = value,
            IsActive = true
        };
        db.Staff.Add(staff);
        return staff;
    }

    private async Task<(Guid Id, string? Name)?> ResolveBranchAsync(Guid? staffBranchId, IReadOnlyDictionary<string, string?> answers, CancellationToken ct)
    {
        if (answers.TryGetValue("branchId", out var branchIdRaw) && Guid.TryParse(branchIdRaw, out var branchGuid))
        {
            var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == branchGuid, ct);
            if (branch is not null)
            {
                return (branch.Id, branch.Name);
            }
        }

        if (answers.TryGetValue("branch", out var branchNameRaw) && !string.IsNullOrWhiteSpace(branchNameRaw))
        {
            var branch = await db.Branches.FirstOrDefaultAsync(b => b.Name == branchNameRaw.Trim(), ct);
            if (branch is not null)
            {
                return (branch.Id, branch.Name);
            }
        }

        if (staffBranchId.HasValue)
        {
            var branch = await db.Branches.FirstOrDefaultAsync(b => b.Id == staffBranchId.Value, ct);
            if (branch is not null)
            {
                return (branch.Id, branch.Name);
            }
        }

        var fallback = await db.Branches.OrderBy(b => b.Name).FirstOrDefaultAsync(ct);
        return fallback is null ? null : (fallback.Id, fallback.Name);
    }

    private static bool TryGetInt(IReadOnlyDictionary<string, string?> source, string key, out int value)
    {
        value = 0;
        if (!source.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static DateTimeOffset? TryGetDateTimeOffset(IReadOnlyDictionary<string, string?> source, string key)
    {
        if (!source.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        return DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizePhone(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? null
            : new string(value.Where(ch => !char.IsWhiteSpace(ch)).ToArray());

    private static bool IsValidLocalPhone(string? value)
        => !string.IsNullOrWhiteSpace(value)
        && value.Length == 8
        && value.All(char.IsDigit);

    private static readonly string[] RequiredKioskKeys = ["phone", "satisfaction", "timeliness", "professionalism"];

    private static bool IsNumericQuestion(string? type)
        => type != null && (
            type.Equals("number", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("nps", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("nps_0_10", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("rating_stars", StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, string?> MergeAnswers(
        Dictionary<string, string?>? answers,
        Dictionary<string, string?>? libertyAnswers)
    {
        var merged = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (answers is not null)
        {
            foreach (var kvp in answers)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        if (libertyAnswers is not null)
        {
            foreach (var kvp in libertyAnswers)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }
        return merged;
    }

    private static string? ResolveMetaValue(string? explicitValue, IReadOnlyDictionary<string, string?> answers, string fallbackKey)
    {
        if (!string.IsNullOrWhiteSpace(explicitValue))
        {
            return explicitValue.Trim();
        }

        return answers.TryGetValue(fallbackKey, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;
    }

    [HttpGet("{code}/nps")]
    public async Task<IActionResult> Nps(string code, CancellationToken ct)
        => (await svc.GetNpsAsync(code, ct)) is { } r ? Ok(r) : NotFound();

    [HttpGet("{code}/definition")]
    public async Task<IActionResult> GetDefinition(string code, CancellationToken ct)
    {
        var definition = await svc.GetPublishedDefinitionAsync(code, ct);
        if (definition is null) return NotFound();

        var json = JsonSerializer.Serialize(definition, DefinitionJsonOptions);
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        var etag = $"\"v{definition.Version}-{Convert.ToHexString(hashBytes)}\"";

        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var candidates))
        {
            foreach (var candidate in candidates.SelectMany(static h => h.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            {
                if (candidate == "*" || string.Equals(candidate, etag, StringComparison.Ordinal))
                {
                    Response.Headers[HeaderNames.ETag] = etag;
                    return StatusCode(StatusCodes.Status304NotModified);
                }
            }
        }

        Response.Headers[HeaderNames.ETag] = etag;
        return Ok(definition);
    }
}
