namespace ClientFlow.Application.DTOs;

public record SurveyDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    bool IsActive,
    string? ThemeJson,
    string? ScopedCss,
    List<SurveySectionDto> Sections,
    List<QuestionListItemDto> Questions);

public record SurveySectionDto(Guid Id, string Title, int Order, int Columns);

public record QuestionDto(
    Guid Id,
    string Type,
    string Prompt,
    string Key,
    bool Required,
    int Order,
    Guid? SectionId,
    string? SettingsJson,
    string? VisibleIf);

public record SubmitResponseDto(Dictionary<string, string?> Data);

public record NpsSummaryDto(int Detractors, int Passives, int Promoters, int Total, int Score);

public record QuestionListItemDto(
    Guid Id,
    Guid? SectionId,
    string Type,
    string Prompt,
    string Key,
    bool Required,
    int Order,
    string? SettingsJson,
    string? VisibleIf);

