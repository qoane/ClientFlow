using ClientFlow.Application.DTOs;
using ClientFlow.Domain.Surveys;
using System.Linq;

namespace ClientFlow.Application.Mapping;

public static class SurveyMapping
{
    public static SurveyDto ToDto(this Survey s) =>
        new SurveyDto(
            s.Id,
            s.Code,
            s.Title,
            s.Description,
            s.IsActive,
            s.ThemeJson,
            s.ScopedCss,
            s.Sections
                .OrderBy(x => x.Order)
                .Select(x => new SurveySectionDto(x.Id, x.Title, x.Order, x.Columns))
                .ToList(),
            s.Questions
                .OrderBy(q => q.Order)
                .Select(q => new QuestionListItemDto(
                    q.Id,
                    q.SectionId,
                    q.Type,
                    q.Prompt,
                    q.Key,
                    q.Required,
                    q.Order,
                    q.SettingsJson))
                .ToList()
        );
}
