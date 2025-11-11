using System.Text.Json.Serialization;

namespace ClientFlow.Web.Analytics;

public sealed record SurveyAnalyticsDto(
    Guid SurveyId,
    string Code,
    string Title,
    int TotalResponses,
    IReadOnlyList<QuestionAnalyticsDto> Questions,
    IReadOnlyList<ResponseRowDto> Responses
);

public sealed record QuestionAnalyticsDto(
    Guid QuestionId,
    string Key,
    string Prompt,
    string? Section,
    string Type,
    int AnswerCount,
    string SummaryKind,
    double? Average,
    double? Minimum,
    double? Maximum,
    IReadOnlyList<ValueCountDto> Buckets,
    IReadOnlyList<TextAnswerDto> TopAnswers
)
{
    [JsonIgnore]
    public bool HasChart => SummaryKind is "scale" or "choice" && Buckets.Count > 0;

    [JsonIgnore]
    public bool HasText => TopAnswers.Count > 0;
}

public sealed record ValueCountDto(string Label, int Count, decimal? NumericValue);

public sealed record TextAnswerDto(string Value, int Count);

public sealed record ResponseRowDto(
    Guid Id,
    DateTimeOffset CreatedUtc,
    IReadOnlyDictionary<string, string?> Answers
);
