using System.Collections.Generic;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClientFlow.Application.Abstractions;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveysController(
    SurveyService svc,
    ISurveyRepository surveys,
    IResponseRepository responses,
    IUnitOfWork uow) : ControllerBase
{
    private static readonly JsonSerializerOptions DefinitionJsonOptions = new(JsonSerializerDefaults.Web);

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

        return Ok(new { id = response.Id });
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
