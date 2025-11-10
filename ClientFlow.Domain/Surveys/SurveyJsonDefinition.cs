using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ClientFlow.Domain.Surveys;

public sealed record class SurveyJsonDefinition
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("code")] public string Code { get; init; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("theme")] public SurveyThemeJson? Theme { get; init; }
    [JsonPropertyName("scopedCss")] public string? ScopedCss { get; init; }
    [JsonPropertyName("style")] public SurveyStyleJson? Style { get; init; }
    [JsonPropertyName("sections")] public List<SurveySectionJson> Sections { get; init; } = [];
    [JsonPropertyName("rules")] public List<SurveyRuleJson> Rules { get; init; } = [];
    [JsonPropertyName("version")] public int? Version { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
}

public sealed record class SurveyThemeJson
{
    [JsonPropertyName("accent")] public string? Accent { get; init; }
    [JsonPropertyName("panel")] public string? Panel { get; init; }
}

public sealed record class SurveyStyleJson
{
    [JsonPropertyName("css")] public string? Css { get; init; }
}

public sealed record class SurveySectionJson
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("order")] public int Order { get; init; }
    [JsonPropertyName("columns")] public int Columns { get; init; } = 1;
    [JsonPropertyName("settings")] public JsonObject? Settings { get; init; }
    [JsonPropertyName("questions")] public List<SurveyQuestionJson> Questions { get; init; } = [];
}

public sealed record class SurveyQuestionJson
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("key")] public string Key { get; init; } = string.Empty;
    [JsonPropertyName("order")] public int Order { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("prompt")] public string Prompt { get; init; } = string.Empty;
    [JsonPropertyName("required")] public bool Required { get; init; }
    [JsonPropertyName("settings")] public JsonObject? Settings { get; init; }
    [JsonPropertyName("options")] public List<SurveyQuestionOptionJson> Options { get; init; } = [];
}

public sealed record class SurveyQuestionOptionJson
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("value")] public string Value { get; init; } = string.Empty;
    [JsonPropertyName("label")] public string Label { get; init; } = string.Empty;
    [JsonPropertyName("order")] public int Order { get; init; }
}

public sealed record class SurveyRuleJson
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("sourceQuestionId")] public string? SourceQuestionId { get; init; }
    [JsonPropertyName("sourceQuestionKey")] public string? SourceQuestionKey { get; init; }
    [JsonPropertyName("condition")] public string Condition { get; init; } = string.Empty;
    [JsonPropertyName("action")] public string Action { get; init; } = string.Empty;
}

public sealed record class SurveyEntityGraph(
    Survey Survey,
    IReadOnlyList<SurveySection> Sections,
    IReadOnlyList<Question> Questions,
    IReadOnlyList<QuestionOption> Options,
    IReadOnlyList<QuestionRule> Rules)
{
    public Survey Survey { get; } = Survey;
    public IReadOnlyList<SurveySection> Sections { get; } = Sections;
    public IReadOnlyList<Question> Questions { get; } = Questions;
    public IReadOnlyList<QuestionOption> Options { get; } = Options;
    public IReadOnlyList<QuestionRule> Rules { get; } = Rules;
}
