using ClientFlow.Application.DTOs;
using ClientFlow.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SurveysController(SurveyService svc) : ControllerBase
{
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

        => (await svc.GetDefinitionByCodeAsync(code, ct)) is { } r ? Ok(r) : NotFound();

}


    
