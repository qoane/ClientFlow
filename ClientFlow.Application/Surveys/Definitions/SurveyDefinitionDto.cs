using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ClientFlow.Application.Surveys.Definitions;

public sealed record SurveyDefinitionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("isActive")] bool IsActive,
    [property: JsonPropertyName("themeAccent")] string? ThemeAccent,
    [property: JsonPropertyName("themePanel")] string? ThemePanel,
    [property: JsonPropertyName("customCss")] string? CustomCss,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("sections")] IReadOnlyList<SectionDto> Sections,
    [property: JsonPropertyName("questions")] IReadOnlyList<QuestionDto> Questions,
    [property: JsonPropertyName("rules")] IReadOnlyList<RuleDto> Rules);

public sealed record SectionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("columns")] int Columns);

public sealed record QuestionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("sectionId")] Guid? SectionId,
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("prompt")] string Prompt,
    [property: JsonPropertyName("required")] bool Required,
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("settingsJson")] string? SettingsJson,
    [property: JsonPropertyName("visibleIf")] string? VisibleIf,
    [property: JsonPropertyName("options")] IReadOnlyList<OptionDto> Options);

public sealed record OptionDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("label")] string Label,
    [property: JsonPropertyName("order")] int Order);

public sealed record RuleDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("sourceQuestionId")] Guid SourceQuestionId,
    [property: JsonPropertyName("condition")] string Condition,
    [property: JsonPropertyName("action")] string Action);
