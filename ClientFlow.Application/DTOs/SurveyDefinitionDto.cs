public record SurveyDefinitionDto(
    Guid Id,
    string Code,
    string Title,
    IEnumerable<SectionDto> Sections,
    IEnumerable<QuestionDto> Questions,
    IEnumerable<OptionDto> Options,
    IEnumerable<RuleDto> Rules,
    ThemeDto? Theme,          // <-- NEW
    StyleDto? Style           // <-- NEW
);
public record ThemeDto(string? Accent, string? Panel);
public record StyleDto(string? Css);
public record SectionDto(Guid Id, string Title, int Order);
public record QuestionDto(
    Guid Id,
    Guid? SectionId,   // <-- make this nullable
    string Type,
    string Prompt,
    string Key,
    bool Required,
    int Order,
    string? SettingsJson
);
public record OptionDto(Guid Id, Guid QuestionId, string Value, string Label, int Order);
public record RuleDto(Guid Id, Guid SourceQuestionId, string Condition, string Action);
