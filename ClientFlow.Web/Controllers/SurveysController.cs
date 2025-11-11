using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClientFlow.Application.Abstractions;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Feedback;
using ClientFlow.Domain.Surveys;
using ClientFlow.Infrastructure;
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

    public record SubmitSurveyRequest(Dictionary<string, string?>? Answers);

    [HttpPost("{code}/submit")]
    public async Task<IActionResult> SubmitSurvey(string code, [FromBody] SubmitSurveyRequest req, CancellationToken ct)
    {
        var survey = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (survey is null || !survey.IsActive)
        {
            return NotFound(new { message = "Survey not found or inactive." });
        }

        var answers = req.Answers ?? new Dictionary<string, string?>();

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
            Channel = "web"
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

            if (question.Type.Equals("number", StringComparison.OrdinalIgnoreCase) ||
                question.Type.Equals("nps", StringComparison.OrdinalIgnoreCase))
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
