using System.Globalization;
using System.Linq;
using ClientFlow.Application.Abstractions;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Mapping;
using ClientFlow.Domain.Surveys;

namespace ClientFlow.Application.Services;

public class SurveyService
{
    private readonly ISurveyRepository _surveys;
    private readonly IResponseRepository _responses;
    private readonly IOptionRepository _options;
    private readonly IRuleRepository _rules;
    private readonly IUnitOfWork _uow;

    public SurveyService(
        ISurveyRepository surveys,
        IResponseRepository responses,
        IOptionRepository options,
        IRuleRepository rules,
        IUnitOfWork uow)
        => (_surveys, _responses, _options, _rules, _uow) = (surveys, responses, options, rules, uow);

    public async Task<SurveyDto?> GetByCodeAsync(string code, CancellationToken ct)
        => (await _surveys.GetByCodeAsync(code, ct))?.ToDto();

    public async Task<Guid?> SubmitAsync(string code, SubmitResponseDto req, CancellationToken ct)
    {
        var s = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return null;

        var resp = new Response
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            Channel = "web"
        };

        foreach (var q in s.Questions)
        {
            // only persist an answer if the key exists in the payload
            if (!req.Data.TryGetValue(q.Key, out var raw) || raw is null) continue;

            var v = raw.Trim();
            if (v.Length == 0) continue;

            var a = new Answer
            {
                Id = Guid.NewGuid(),
                ResponseId = resp.Id,
                QuestionId = q.Id
            };

            // Treat any "nps*" question as numeric
            if (q.Type is not null &&
                q.Type.StartsWith("nps", StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
            {
                a.ValueNumber = n;
            }
            else
            {
                a.ValueText = v;
            }

            resp.Answers.Add(a);
        }

        // If nothing to save, bail out gracefully
        if (resp.Answers.Count == 0) return null;

        await _responses.AddAsync(resp, ct);
        await _uow.SaveChangesAsync(ct);
        return resp.Id;
    }

    public async Task<NpsSummaryDto?> GetNpsAsync(string code, CancellationToken ct)
    {
        var s = await _surveys.GetByCodeAsync(code, ct);
        if (s is null) return null;

        var scores = await _responses.GetNpsScoresAsync(s.Id, ct);
        int detr = scores.Count(x => x <= 6);
        int pass = scores.Count(x => x is 7 or 8);
        int prom = scores.Count(x => x >= 9);
        int total = scores.Count;
        int score = total == 0 ? 0 : (int)Math.Round(((prom - detr) / (double)total) * 100);

        return new NpsSummaryDto(detr, pass, prom, total, score);
    }

    // Definition for the runner (sections, questions, options, rules)
    public async Task<SurveyDefinitionDto?> GetDefinitionByCodeAsync(string code, CancellationToken ct = default)
    {
        var s = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return null;

        var qIds = s.Questions.Select(q => q.Id).ToList();
        var options = await _options.GetByQuestionIdsAsync(qIds, ct);
        var rules = await _rules.GetBySurveyIdAsync(s.Id, ct);

        return new SurveyDefinitionDto(
     s.Id,
     s.Code,
     s.Title,
     s.Sections
         .OrderBy(x => x.Order)
         .Select(x => new SectionDto(x.Id, x.Title, x.Order)),
     s.Questions
         .OrderBy(x => x.Order)
         .Select(q => new QuestionDto(
             q.Id,
             q.SectionId,
             q.Type,
             q.Prompt,
             q.Key,
             q.Required,
             q.Order,
             string.IsNullOrWhiteSpace(q.SettingsJson) ? null : q.SettingsJson)),
     options
         .OrderBy(o => o.Order)
         .Select(o => new OptionDto(o.Id, o.QuestionId, o.Value, o.Label, o.Order)),
     rules.Select(r => new RuleDto(r.Id, r.SourceQuestionId, r.Condition, r.Action)),
     new ThemeDto(s.ThemeAccent, s.ThemePanel),        // <-- map stored theme
     new StyleDto(s.CustomCss)                         // <-- map stored CSS (raw)
 );

    }

}
