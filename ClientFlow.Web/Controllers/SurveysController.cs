using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveysController(SurveyService svc) : ControllerBase
{
    private static readonly JsonSerializerOptions DefinitionJsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("{code}")]
    public async Task<IActionResult> Get(string code, CancellationToken ct)
        => (await svc.GetByCodeAsync(code, ct)) is { } s ? Ok(s) : NotFound();

    [HttpPost("{code}/responses")]
    public async Task<IActionResult> Submit(string code, [FromBody] SubmitResponseDto dto, CancellationToken ct)
        => (await svc.SubmitAsync(code, dto, ct)) is { } id ? Ok(new { id }) : NotFound();

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
