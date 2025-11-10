using System.Collections.Generic;
using System.Linq;
using ClientFlow.Domain.Surveys;

namespace ClientFlow.Application.Surveys.Definitions;

public static class SurveyDefinitionMapper
{
    public static SurveyDefinitionDto FromEntities(
        Survey survey,
        IEnumerable<QuestionOption> options,
        IEnumerable<QuestionRule> rules,
        int version = 1)
    {
        ArgumentNullException.ThrowIfNull(survey);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rules);

        var sectionDtos = survey.Sections
            .OrderBy(s => s.Order)
            .ThenBy(s => s.Title)
            .Select(s => new SectionDto(s.Id, s.Title, s.Order, s.Columns))
            .ToArray();

        var optionLookup = options
            .GroupBy(o => o.QuestionId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<OptionDto>)g
                    .OrderBy(o => o.Order)
                    .ThenBy(o => o.Value)
                    .Select(o => new OptionDto(o.Id, o.Value, o.Label, o.Order))
                    .ToArray());

        var questionDtos = survey.Questions
            .OrderBy(q => q.Order)
            .ThenBy(q => q.Key)
            .Select(q => new QuestionDto(
                q.Id,
                q.SectionId,
                q.Key,
                q.Type,
                q.Prompt,
                q.Required,
                q.Order,
                q.SettingsJson,
                optionLookup.TryGetValue(q.Id, out var opts) ? opts : Array.Empty<OptionDto>()))
            .ToArray();

        var ruleDtos = rules
            .OrderBy(r => r.Id)
            .Select(r => new RuleDto(r.Id, r.SourceQuestionId, r.Condition, r.Action))
            .ToArray();

        return new SurveyDefinitionDto(
            survey.Id,
            survey.Code,
            survey.Title,
            survey.IsActive,
            survey.ThemeAccent,
            survey.ThemePanel,
            survey.CustomCss,
            version,
            sectionDtos,
            questionDtos,
            ruleDtos);
    }
}
