using ClientFlow.Application.Abstractions;
using ClientFlow.Application.Services;
using ClientFlow.Domain.Surveys;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ClientFlow.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    ISurveyRepository surveys,
    IResponseRepository responses,
    IOptionRepository options,
    IRuleRepository rules,
    IUnitOfWork uow,
    SurveyService svc) : ControllerBase
{
    [HttpGet("surveys")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var list = await surveys.ListAsync(ct);
        // project to a light shape (avoid large graphs / cycles)
        var dto = list.Select(s => new {
            s.Id,
            s.Code,
            s.Title,
            s.IsActive,
            s.Description
        });
        return Ok(dto);
    }

    // ---------- CREATE ----------
    // POST /api/admin/surveys
    public record CreateSurveyReq(string Code, string Title, string? Description);

    [HttpPost("surveys")]
    public async Task<IActionResult> Create([FromBody] CreateSurveyReq req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Code) || string.IsNullOrWhiteSpace(req.Title))
            return BadRequest("Code and Title are required.");

        var code = req.Code.Trim();
        if (await surveys.GetByCodeAsync(code, ct) is not null)
            return Conflict("A survey with that code already exists.");

        var s = new Survey
        {
            Id = Guid.NewGuid(),
            Code = code,
            Title = req.Title.Trim(),
            Description = req.Description,
            IsActive = true
        };

        await surveys.AddAsync(s, ct);
        await uow.SaveChangesAsync(ct);
        return Created($"/api/admin/surveys/{s.Code}", new { s.Id, s.Code, s.Title, s.IsActive });
    }

    // ---------- ACTIVATE/DEACTIVATE (by code) ----------
    // PUT /api/admin/surveys/{code}/active
    public record SetActiveReq(bool IsActive);

    [HttpPut("surveys/{code}/active")]
    public async Task<IActionResult> SetActive(string code, [FromBody] SetActiveReq req, CancellationToken ct)
    {
        var s = await surveys.GetByCodeAsync(code, ct);
        if (s is null) return NotFound();

        s.IsActive = req.IsActive;
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- UPDATE META (title/description) ----------
    // PUT /api/admin/surveys/{code}
    public record UpdateSurveyReq(string? Title, string? Description);

    [HttpPut("surveys/{code}")]
    public async Task<IActionResult> UpdateMeta(string code, [FromBody] UpdateSurveyReq req, CancellationToken ct)
    {
        var s = await surveys.GetByCodeAsync(code, ct);
        if (s is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(req.Title)) s.Title = req.Title!.Trim();
        if (req.Description is not null) s.Description = req.Description;
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---------- (kept) your existing toggle by Id ----------
    // POST /api/admin/surveys/{id}/toggle?active=true
    [HttpPost("surveys/{id:guid}/toggle")]
    public async Task<IActionResult> Toggle(Guid id, [FromQuery] bool active, CancellationToken ct)
    {
        var all = await surveys.ListAsync(ct);
        var s = all.FirstOrDefault(x => x.Id == id);
        if (s is null) return NotFound();

        s.IsActive = active;
        await surveys.SaveChangesAsync(ct); // if your UoW is required here, swap to uow.SaveChangesAsync(ct)
        return Ok();
    }

    [HttpGet("surveys/{code}/definition")]
    public async Task<IActionResult> GetDefinition(string code, CancellationToken ct)
    {
        var def = await svc.GetDefinitionByCodeAsync(code, ct);
        return Ok(def ?? (object)new { });   // cast makes both sides 'object'
    }


    // ---------- DESIGNER: add section ----------
    // in AdminController
    public record AddSectionReq(string Title, int Order = 0, int Columns = 1);

    [HttpPost("surveys/{code}/sections")]
    public async Task<IActionResult> AddSection(
        string code,
        [FromBody] AddSectionReq req,
        [FromServices] ISectionRepository sectionsRepo,  // inject repo
        CancellationToken ct)
    {
        var s = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return NotFound();

        var sec = new SurveySection
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            Title = string.IsNullOrWhiteSpace(req.Title) ? "Section" : req.Title.Trim(),
            Order = req.Order,
            Columns = (req.Columns is <= 0 or > 2) ? 1 : req.Columns
        };

        await sectionsRepo.AddAsync(sec, ct);  // <-- forces INSERT
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }


    // ---------- DESIGNER: add question ----------
    public record AddQuestionReq(
        Guid SectionId,
        string Type,
        string Prompt,
        string Key,
        bool Required = false,
        int Order = 0,
        string? SettingsJson = null);

    // POST /api/admin/surveys/{code}/questions
    // POST /api/admin/surveys/{code}/questions
    [HttpPost("surveys/{code}/questions")]
    public async Task<IActionResult> AddQuestion(
        string code,
        [FromBody] AddQuestionReq req,
        [FromServices] IQuestionRepository questions,   // <-- inject
        CancellationToken ct)
    {
        var s = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return NotFound();

        // ensure key uniqueness inside survey
        if (s.Questions.Any(q => q.Key.Equals(req.Key, StringComparison.OrdinalIgnoreCase)))
            return Conflict("Key already used in this survey.");

        var q = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            SectionId = req.SectionId,
            Order = req.Order,
            Type = req.Type,
            Prompt = req.Prompt,
            Key = req.Key,
            Required = req.Required,
            SettingsJson = req.SettingsJson
        };

        await questions.AddAsync(q, ct);   // <-- guarantees EntityState.Added → INSERT
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }


    // ---------- DESIGNER: add option ----------
    public record AddOptionReq(Guid QuestionId, string Value, string Label, int Order = 0);

    // POST /api/admin/surveys/{code}/options
    [HttpPost("surveys/{code}/options")]
    public async Task<IActionResult> AddOption(string code, [FromBody] AddOptionReq req, CancellationToken ct)
    {
        // optional: verify the survey exists
        if (await surveys.GetByCodeAsync(code, ct) is null) return NotFound();

        await options.AddAsync(new QuestionOption
        {
            Id = Guid.NewGuid(),
            QuestionId = req.QuestionId,
            Value = req.Value,
            Label = req.Label,
            Order = req.Order
        }, ct);

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    public record UpdateStyleReq(string? Accent, string? Panel, string? Css, string? CssBase64);

    [HttpPut("surveys/{code}/style")]
public async Task<IActionResult> UpdateStyle(string code, [FromBody] UpdateStyleReq req, CancellationToken ct)
{
    var s = await surveys.GetByCodeForUpdateAsync(code, ct);
    if (s is null) return NotFound();

    // Only overwrite when values are provided
    if (!string.IsNullOrWhiteSpace(req.Accent)) s.ThemeAccent = req.Accent.Trim();
    if (!string.IsNullOrWhiteSpace(req.Panel))  s.ThemePanel  = req.Panel.Trim();

    if (req.CssBase64 is not null)
        s.CustomCss = Encoding.UTF8.GetString(Convert.FromBase64String(req.CssBase64));
    else if (req.Css is not null)
        s.CustomCss = req.Css; // allow empty string to clear CSS if caller wants

    await uow.SaveChangesAsync(ct);
    return NoContent();
}


    public record SubmitReq(Dictionary<string, string?> Answers);

    // POST /api/surveys/{code}/submit
    [HttpPost("{code}/submit")]
    public async Task<IActionResult> Submit(string code, [FromBody] SubmitReq req, CancellationToken ct)
    {
        // load survey with questions so we can map keys -> QuestionId
        var s = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null || !s.IsActive) return NotFound("Survey not found or inactive.");

        // basic required check
        var missing = s.Questions
            .Where(q => q.Required)
            .Where(q => !req.Answers.TryGetValue(q.Key, out var v) || string.IsNullOrWhiteSpace(v))
            .Select(q => q.Key)
            .ToArray();
        if (missing.Length > 0) return BadRequest(new { message = "Missing required answers.", missing });

        var byKey = s.Questions.ToDictionary(q => q.Key, StringComparer.OrdinalIgnoreCase);

        var resp = new Response
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            Channel = "web"
        };

        foreach (var kv in req.Answers)
        {
            if (!byKey.TryGetValue(kv.Key, out var q)) continue; // ignore unknown keys

            var a = new Answer
            {
                Id = Guid.NewGuid(),
                Response = resp,       // sets ResponseId via EF
                QuestionId = q.Id
            };

            // store numbers in ValueNumber when appropriate, else ValueText
            if (q.Type.Equals("number", StringComparison.OrdinalIgnoreCase) ||
                q.Type.Equals("nps", StringComparison.OrdinalIgnoreCase))
            {
                if (decimal.TryParse(kv.Value, out var d)) a.ValueNumber = d;
                else a.ValueText = kv.Value;
            }
            else
            {
                a.ValueText = kv.Value;
            }

            resp.Answers.Add(a);
        }

        await responses.AddAsync(resp, ct);
        await uow.SaveChangesAsync(ct);

        return Created($"/api/surveys/{code}/responses/{resp.Id}", new { resp.Id });
    }

}
