using ClientFlow.Application.Abstractions;
using ClientFlow.Application.Services;
using ClientFlow.Application.Surveys.Validation;
using ClientFlow.Domain.Surveys;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using ClientFlow.Infrastructure;

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

    [HttpPost("surveys/{code}/publish")]
    public async Task<IActionResult> Publish(string code, CancellationToken ct)
    {
        var version = await svc.PublishSurveyAsync(code, ct);
        if (version is null) return NotFound();
        return Ok(new { version });
    }

    [HttpGet("surveys/{code}/versions")]
    public async Task<IActionResult> ListVersions(string code, CancellationToken ct)
    {
        var versions = await svc.GetSurveyVersionsAsync(code, ct);
        if (versions is null) return NotFound();
        return Ok(versions);
    }

    [HttpPost("surveys/{code}/rollback")]
    public async Task<IActionResult> Rollback(string code, [FromQuery] int version, CancellationToken ct)
    {
        if (version <= 0) return BadRequest("Version must be greater than zero.");

        var success = await svc.SetPublishedVersionAsync(code, version, ct);
        if (!success) return NotFound();
        return Ok(new { version });
    }


    public record DesignerThemeDto(string? Accent, string? Panel);
    public record DesignerSectionDto(Guid? Id, string? Title, int Order, int Columns);
    public record DesignerOptionDto(Guid? Id, Guid QuestionId, string Value, string Label, int Order);
    public record DesignerQuestionDto(
        Guid? Id,
        Guid? SectionId,
        string Type,
        string Prompt,
        string Key,
        bool Required,
        int Order,
        Dictionary<string, object?>? Settings,
        List<DesignerOptionDto>? Choices,
        List<string>? Validations,
        string? Visibility);
    public record DesignerRuleDto(Guid? Id, Guid SourceQuestionId, string Condition, string Action);
    public record SaveSurveyDefinitionReq(
        string? Code,
        string? Title,
        string? Description,
        DesignerThemeDto? Theme,
        List<DesignerSectionDto> Sections,
        List<DesignerQuestionDto> Questions,
        List<DesignerOptionDto>? Options,
        List<DesignerRuleDto>? Rules);

    [HttpPut("surveys/{code}/definition")]
    public async Task<IActionResult> SaveDefinition(
        string code,
        [FromBody] SaveSurveyDefinitionReq req,
        [FromServices] AppDbContext db,
        CancellationToken ct)
    {
        var survey = await db.Surveys
            .Include(s => s.Sections)
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Code == code, ct);

        if (survey is null) return NotFound();

        var existingQuestionIds = survey.Questions.Select(q => q.Id).ToList();
        if (existingQuestionIds.Count > 0)
        {
            var existingOptions = await db.Options.Where(o => existingQuestionIds.Contains(o.QuestionId)).ToListAsync(ct);
            db.Options.RemoveRange(existingOptions);
        }

        var existingRules = await db.Rules.Where(r => r.SurveyId == survey.Id).ToListAsync(ct);
        db.Rules.RemoveRange(existingRules);

        db.Questions.RemoveRange(survey.Questions);
        db.Sections.RemoveRange(survey.Sections);
        await db.SaveChangesAsync(ct);

        var sectionMap = new Dictionary<Guid, Guid>();
        var sectionEntities = new List<SurveySection>();
        if (req.Sections is { Count: > 0 })
        {
            var orderedSections = req.Sections
                .OrderBy(s => s.Order)
                .Select((sec, index) => (sec, index));

            foreach (var (sec, idx) in orderedSections)
            {
                var id = sec.Id ?? Guid.NewGuid();
                sectionMap[id] = id;
                sectionEntities.Add(new SurveySection
                {
                    Id = id,
                    SurveyId = survey.Id,
                    Title = string.IsNullOrWhiteSpace(sec.Title) ? $"Section {idx + 1}" : sec.Title.Trim(),
                    Order = idx + 1,
                    Columns = sec.Columns <= 0 ? 1 : Math.Min(sec.Columns, 2)
                });
            }
        }

        var questionEntities = new List<Question>();
        var questionMap = new Dictionary<Guid, Guid>();
        if (req.Questions is { Count: > 0 })
        {
            var orderedQuestions = req.Questions
                .OrderBy(q => q.Order)
                .Select((question, index) => (question, index));

            foreach (var (q, idx) in orderedQuestions)
            {
                var qId = q.Id ?? Guid.NewGuid();
                questionMap[qId] = qId;

                Guid? sectionId = null;
                if (q.SectionId.HasValue)
                {
                    sectionId = sectionMap.TryGetValue(q.SectionId.Value, out var mapped) ? mapped : q.SectionId;
                }

                Dictionary<string, object?>? settingsDict = null;
                if (q.Settings is { Count: > 0 })
                {
                    settingsDict = new Dictionary<string, object?>(q.Settings, StringComparer.OrdinalIgnoreCase);
                    settingsDict.Remove("choices");
                }
                if (q.Validations is { Count: > 0 })
                {
                    settingsDict ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    settingsDict["validations"] = q.Validations;
                }
                if (!string.IsNullOrWhiteSpace(q.Visibility))
                {
                    settingsDict ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    settingsDict["visibility"] = q.Visibility.Trim();
                }

                questionEntities.Add(new Question
                {
                    Id = qId,
                    SurveyId = survey.Id,
                    SectionId = sectionId,
                    Type = q.Type,
                    Prompt = q.Prompt,
                    Key = q.Key,
                    Required = q.Required,
                    Order = idx + 1,
                    SettingsJson = settingsDict is { Count: > 0 }
                        ? JsonSerializer.Serialize(settingsDict)
                        : null
                });
            }
        }

        var optionEntities = new List<QuestionOption>();
        if (req.Options is { Count: > 0 })
        {
            foreach (var opt in req.Options.OrderBy(o => o.Order))
            {
                var targetQuestionId = questionMap.TryGetValue(opt.QuestionId, out var mapped) ? mapped : opt.QuestionId;
                optionEntities.Add(new QuestionOption
                {
                    Id = opt.Id ?? Guid.NewGuid(),
                    QuestionId = targetQuestionId,
                    Value = opt.Value,
                    Label = opt.Label,
                    Order = opt.Order
                });
            }
        }
        else if (req.Questions is { Count: > 0 })
        {
            foreach (var q in req.Questions)
            {
                if (q.Choices is not { Count: > 0 }) continue;
                foreach (var (choice, index) in q.Choices.Select((c, i) => (c, i)))
                {
                    var targetQuestionId = questionMap.TryGetValue(q.Id ?? Guid.Empty, out var mapped)
                        ? mapped
                        : (q.Id ?? Guid.Empty);
                    if (targetQuestionId == Guid.Empty) continue;
                    optionEntities.Add(new QuestionOption
                    {
                        Id = choice.Id ?? Guid.NewGuid(),
                        QuestionId = targetQuestionId,
                        Value = choice.Value,
                        Label = choice.Label,
                        Order = choice.Order != 0 ? choice.Order : index + 1
                    });
                }
            }
        }

        var ruleEntities = new List<QuestionRule>();
        if (req.Rules is { Count: > 0 })
        {
            foreach (var rule in req.Rules)
            {
                var sourceId = questionMap.TryGetValue(rule.SourceQuestionId, out var mapped)
                    ? mapped
                    : rule.SourceQuestionId;
                ruleEntities.Add(new QuestionRule
                {
                    Id = rule.Id ?? Guid.NewGuid(),
                    SurveyId = survey.Id,
                    SourceQuestionId = sourceId,
                    Condition = rule.Condition,
                    Action = rule.Action
                });
            }
        }

        if (sectionEntities.Count > 0)
            await db.Sections.AddRangeAsync(sectionEntities, ct);
        if (questionEntities.Count > 0)
            await db.Questions.AddRangeAsync(questionEntities, ct);
        if (optionEntities.Count > 0)
            await db.Options.AddRangeAsync(optionEntities, ct);
        if (ruleEntities.Count > 0)
            await db.Rules.AddRangeAsync(ruleEntities, ct);

        if (!string.IsNullOrWhiteSpace(req.Title)) survey.Title = req.Title.Trim();
        if (req.Description is not null) survey.Description = req.Description;
        if (req.Theme is not null)
        {
            if (!string.IsNullOrWhiteSpace(req.Theme.Accent)) survey.ThemeAccent = req.Theme.Accent.Trim();
            if (!string.IsNullOrWhiteSpace(req.Theme.Panel)) survey.ThemePanel = req.Theme.Panel.Trim();
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }


    // ---------- DESIGNER: add section ----------
    // in AdminController
    public record AddSectionReq(string Title, int Order = 0, int Columns = 1);
    public record UpdateSectionOrderReq(int Order);

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

    [HttpPut("sections/{id:guid}/order")]
    public async Task<IActionResult> UpdateSectionOrder(
        Guid id,
        [FromBody] UpdateSectionOrderReq req,
        [FromServices] ISectionRepository sectionsRepo,
        CancellationToken ct)
    {
        var section = await sectionsRepo.GetByIdAsync(id, ct);
        if (section is null) return NotFound();

        var sections = await sectionsRepo.GetBySurveyIdAsync(section.SurveyId, ct);
        var ordered = sections.OrderBy(s => s.Order).ToList();
        var current = ordered.FirstOrDefault(s => s.Id == id);
        if (current is null) return NotFound();

        ordered.Remove(current);
        var targetIndex = req.Order < 0 ? 0 : req.Order;
        if (targetIndex > ordered.Count) targetIndex = ordered.Count;
        ordered.Insert(targetIndex, current);

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }


    // ---------- DESIGNER: add question ----------
    public record AddQuestionReq(
        Guid SectionId,
        string Type,
        string? Prompt,
        string Key,
        bool Required = false,
        int Order = 0,
        string? SettingsJson = null);

    public record UpdateQuestionReq(
        string? Prompt,
        bool? Required,
        int? Order,
        string? SettingsJson,
        Guid? SectionId,
        string? Type);

    public record UpdateQuestionOrderReq(int Order);
    public record MoveQuestionReq(Guid SectionId, int Order);

    // POST /api/admin/surveys/{code}/questions
    // POST /api/admin/surveys/{code}/questions
    [HttpPost("surveys/{code}/questions")]
    public async Task<IActionResult> AddQuestion(
        string code,
        [FromBody] AddQuestionReq req,
        [FromServices] IQuestionRepository questions,   // <-- inject
        [FromServices] IQuestionSettingsValidator settingsValidator,
        CancellationToken ct)
    {
        var s = await surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return NotFound();

        // ensure key uniqueness inside survey
        if (string.IsNullOrWhiteSpace(req.Key))
            return BadRequest("Key is required.");

        var key = req.Key.Trim();

        if (s.Questions.Any(q => q.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            return Conflict("Key already used in this survey.");

        var section = s.Sections.FirstOrDefault(sec => sec.Id == req.SectionId);
        if (section is null)
            return BadRequest("Section does not belong to this survey.");

        if (string.IsNullOrWhiteSpace(req.Type))
            return BadRequest("Type is required.");

        var type = req.Type.Trim();
        var isStatic = type.StartsWith("static_", StringComparison.OrdinalIgnoreCase);

        var prompt = req.Prompt?.Trim();
        if (!isStatic && string.IsNullOrWhiteSpace(prompt))
            return BadRequest("Prompt is required for non static question types.");

        var validation = settingsValidator.Validate(type, req.SettingsJson);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        var q = new Question
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            SectionId = req.SectionId,
            Order = req.Order,
            Type = type,
            Prompt = prompt ?? string.Empty,
            Key = key,
            Required = isStatic ? false : req.Required,
            SettingsJson = req.SettingsJson
        };

        await questions.AddAsync(q, ct);   // <-- guarantees EntityState.Added → INSERT
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("questions/{id:guid}/order")]
    public async Task<IActionResult> UpdateQuestionOrder(
        Guid id,
        [FromBody] UpdateQuestionOrderReq req,
        [FromServices] IQuestionRepository questions,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(id, ct);
        if (question is null) return NotFound();
        if (question.SectionId is null)
            return BadRequest("Question is not assigned to a section.");

        var siblings = await questions.GetBySectionIdAsync(question.SectionId.Value, ct);
        var ordered = siblings.OrderBy(q => q.Order).ToList();
        var current = ordered.FirstOrDefault(q => q.Id == id);
        if (current is null) return NotFound();

        ordered.Remove(current);
        var targetIndex = req.Order < 0 ? 0 : req.Order;
        if (targetIndex > ordered.Count) targetIndex = ordered.Count;
        ordered.Insert(targetIndex, current);

        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].Order = i;
        }

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("questions/{id:guid}/move")]
    public async Task<IActionResult> MoveQuestion(
        Guid id,
        [FromBody] MoveQuestionReq req,
        [FromServices] IQuestionRepository questions,
        [FromServices] ISectionRepository sectionsRepo,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(id, ct);
        if (question is null) return NotFound();

        var destination = await sectionsRepo.GetByIdAsync(req.SectionId, ct);
        if (destination is null) return NotFound();

        if (question.SurveyId != destination.SurveyId)
            return BadRequest("Section does not belong to the same survey as the question.");

        if (question.SectionId == req.SectionId)
        {
            var sameSectionQuestions = await questions.GetBySectionIdAsync(req.SectionId, ct);
            var sameOrdered = sameSectionQuestions.OrderBy(q => q.Order).ToList();
            var current = sameOrdered.FirstOrDefault(q => q.Id == id);
            if (current is null) return NotFound();

            sameOrdered.Remove(current);
            var sameTarget = req.Order < 0 ? 0 : req.Order;
            if (sameTarget > sameOrdered.Count) sameTarget = sameOrdered.Count;
            sameOrdered.Insert(sameTarget, current);

            for (var i = 0; i < sameOrdered.Count; i++)
            {
                sameOrdered[i].Order = i;
            }

            await uow.SaveChangesAsync(ct);
            return NoContent();
        }

        if (question.SectionId is Guid currentSectionId)
        {
            var currentSectionQuestions = await questions.GetBySectionIdAsync(currentSectionId, ct);
            var currentOrdered = currentSectionQuestions.OrderBy(q => q.Order).ToList();
            var current = currentOrdered.FirstOrDefault(q => q.Id == id);
            if (current is not null)
            {
                currentOrdered.Remove(current);
                for (var i = 0; i < currentOrdered.Count; i++)
                {
                    currentOrdered[i].Order = i;
                }
            }
        }

        var destinationQuestions = await questions.GetBySectionIdAsync(req.SectionId, ct);
        var destinationOrdered = destinationQuestions.OrderBy(q => q.Order).ToList();

        question.SectionId = req.SectionId;

        var destinationIndex = req.Order < 0 ? 0 : req.Order;
        if (destinationIndex > destinationOrdered.Count) destinationIndex = destinationOrdered.Count;
        destinationOrdered.Insert(destinationIndex, question);

        for (var i = 0; i < destinationOrdered.Count; i++)
        {
            destinationOrdered[i].Order = i;
        }

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPut("questions/{id:guid}")]
    public async Task<IActionResult> UpdateQuestion(
        Guid id,
        [FromBody] UpdateQuestionReq req,
        [FromServices] IQuestionRepository questions,
        [FromServices] ISectionRepository sectionsRepo,
        [FromServices] IQuestionSettingsValidator settingsValidator,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(id, ct);
        if (question is null) return NotFound();

        var pendingSettingsJson = req.SettingsJson;

        if (req.Type is not null)
        {
            if (string.IsNullOrWhiteSpace(req.Type))
                return BadRequest("Type cannot be empty.");

            question.Type = req.Type.Trim();
        }

        var isStatic = question.Type.StartsWith("static_", StringComparison.OrdinalIgnoreCase);

        if (req.Prompt is not null)
        {
            var prompt = req.Prompt.Trim();
            if (!isStatic && string.IsNullOrWhiteSpace(prompt))
                return BadRequest("Prompt is required for non static question types.");

            question.Prompt = string.IsNullOrWhiteSpace(prompt) ? string.Empty : prompt;
        }
        else if (!isStatic && string.IsNullOrWhiteSpace(question.Prompt))
        {
            return BadRequest("Prompt is required for non static question types.");
        }

        if (req.Required.HasValue)
            question.Required = req.Required.Value;

        if (req.Order.HasValue)
            question.Order = req.Order.Value;

        if (req.SectionId.HasValue)
        {
            var section = await sectionsRepo.GetByIdAsync(req.SectionId.Value, ct);
            if (section is null) return NotFound("Section not found.");
            if (section.SurveyId != question.SurveyId)
                return Conflict("Section does not belong to the same survey.");

            question.SectionId = section.Id;
        }

        var validation = settingsValidator.Validate(question.Type, pendingSettingsJson ?? question.SettingsJson);
        if (!validation.IsValid)
            return BadRequest(validation.ErrorMessage);

        if (pendingSettingsJson is not null)
            question.SettingsJson = pendingSettingsJson;

        if (isStatic)
            question.Required = false;

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("questions/{id:guid}")]
    public async Task<IActionResult> DeleteQuestion(
        Guid id,
        [FromServices] IQuestionRepository questions,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(id, ct);
        if (question is null) return NotFound();

        var optionList = await options.GetByQuestionIdsAsync(new[] { id }, ct);
        if (optionList.Count > 0)
            options.RemoveRange(optionList);

        var ruleList = await rules.GetBySurveyIdAsync(question.SurveyId, ct);
        var rulesToRemove = ruleList.Where(r => r.SourceQuestionId == id).ToList();
        if (rulesToRemove.Count > 0)
            rules.RemoveRange(rulesToRemove);

        questions.Remove(question);
        await uow.SaveChangesAsync(ct);
        return NoContent();
    }


    // ---------- DESIGNER: add option ----------
    public record AddOptionReq(Guid QuestionId, string Value, string Label, int Order = 0);
    public record ReplaceOptionReq(string Value, string Label, int Order = 0);

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

    // PUT /api/admin/questions/{id}/options
    [HttpPut("questions/{id:guid}/options")]
    public async Task<IActionResult> ReplaceOptions(
        Guid id,
        [FromBody] List<ReplaceOptionReq> req,
        [FromServices] IQuestionRepository questions,
        CancellationToken ct)
    {
        var question = await questions.GetByIdAsync(id, ct);
        if (question is null) return NotFound();

        var existing = await options.GetByQuestionIdsAsync(new[] { id }, ct);
        if (existing.Count > 0)
            options.RemoveRange(existing);

        if (req is { Count: > 0 })
        {
            foreach (var option in req.OrderBy(o => o.Order))
            {
                await options.AddAsync(new QuestionOption
                {
                    Id = Guid.NewGuid(),
                    QuestionId = id,
                    Value = option.Value,
                    Label = option.Label,
                    Order = option.Order
                }, ct);
            }
        }

        await uow.SaveChangesAsync(ct);
        return NoContent();
    }

    // DELETE /api/admin/options/{id}
    [HttpDelete("options/{id:guid}")]
    public async Task<IActionResult> DeleteOption(Guid id, CancellationToken ct)
    {
        var option = await options.GetByIdAsync(id, ct);
        if (option is null) return NotFound();

        options.Remove(option);
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
