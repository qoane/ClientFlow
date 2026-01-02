using System.Globalization;
using ClientFlow.Domain.Feedback;
using ClientFlow.Domain.Surveys;

namespace ClientFlow.Web.Analytics;

public static class LegacyKioskAnalyticsBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private sealed record LegacyQuestion(string Key, string Prompt, string Type, bool IsMulti = false);

    private static readonly IReadOnlyList<LegacyQuestion> Questions =
    [
        new("phone", "Phone", "text"),
        new("staff", "Staff", "single"),
        new("branch", "Branch", "single"),
        new("serviceType", "Service Type", "single"),
        new("gender", "Gender", "single"),
        new("ageRange", "Age Range", "single"),
        new("city", "City", "single"),
        new("policies", "Policies", "multi", true),
        new("timeRating", "Time Rating", "number"),
        new("overallRating", "Overall Rating", "number"),
        new("respectRating", "Respect Rating", "number"),
        new("recommendRating", "Recommend Rating", "number"),
        new("contactPreference", "Contact Preference", "single"),
        new("comment", "Comment", "text"),
        new("durationSeconds", "Duration (seconds)", "number")
    ];

    public static SurveyAnalyticsDto Build(Survey survey, IReadOnlyList<KioskFeedback> feedback)
    {
        var sectionLabel = string.IsNullOrWhiteSpace(survey.Title) ? "Legacy Survey" : survey.Title;
        var answerRows = feedback
            .OrderByDescending(x => x.CreatedUtc)
            .Select(x => new ResponseRowDto(
                x.Id,
                x.CreatedUtc,
                x.StartedUtc,
                x.DurationSeconds,
                null,
                null,
                BuildAnswerMap(x)))
            .ToList();

        var summaries = new List<QuestionAnalyticsDto>();

        foreach (var question in Questions)
        {
            var answers = feedback.SelectMany(x => ExtractAnswerValues(x, question)).ToList();
            var (summaryKind, buckets, average, minimum, maximum, topText) = Summarize(question, answers);

            summaries.Add(new QuestionAnalyticsDto(
                Guid.Empty,
                question.Key,
                question.Prompt,
                sectionLabel,
                question.Type,
                answers.Count,
                summaryKind,
                average,
                minimum,
                maximum,
                buckets,
                topText));
        }

        return new SurveyAnalyticsDto(
            survey.Id,
            survey.Code,
            survey.Title,
            feedback.Count,
            summaries,
            answerRows);
    }

    private static IReadOnlyDictionary<string, string?> BuildAnswerMap(KioskFeedback entry)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["phone"] = entry.Phone,
            ["staff"] = FormatStaffLabel(entry),
            ["branch"] = entry.BranchName,
            ["serviceType"] = entry.ServiceType,
            ["gender"] = entry.Gender,
            ["ageRange"] = entry.AgeRange,
            ["city"] = entry.City,
            ["policies"] = string.Join(", ", ParsePolicies(entry.PoliciesJson)),
            ["timeRating"] = FormatNumber(entry.TimeRating),
            ["overallRating"] = FormatNumber(entry.OverallRating),
            ["respectRating"] = FormatNumber(entry.RespectRating),
            ["recommendRating"] = entry.RecommendRating.HasValue ? FormatNumber(entry.RecommendRating.Value) : null,
            ["contactPreference"] = entry.ContactPreference,
            ["comment"] = entry.Comment,
            ["durationSeconds"] = entry.DurationSeconds.ToString(Culture)
        };

        return map;
    }

    private static IEnumerable<string> ExtractAnswerValues(KioskFeedback entry, LegacyQuestion question)
    {
        return question.Key switch
        {
            "phone" => Maybe(entry.Phone),
            "staff" => Maybe(FormatStaffLabel(entry)),
            "branch" => Maybe(entry.BranchName),
            "serviceType" => Maybe(entry.ServiceType),
            "gender" => Maybe(entry.Gender),
            "ageRange" => Maybe(entry.AgeRange),
            "city" => Maybe(entry.City),
            "policies" => ParsePolicies(entry.PoliciesJson),
            "timeRating" => entry.TimeRating > 0 ? [entry.TimeRating.ToString(Culture)] : [],
            "overallRating" => entry.OverallRating > 0 ? [entry.OverallRating.ToString(Culture)] : [],
            "respectRating" => entry.RespectRating > 0 ? [entry.RespectRating.ToString(Culture)] : [],
            "recommendRating" => entry.RecommendRating.HasValue ? [entry.RecommendRating.Value.ToString(Culture)] : [],
            "contactPreference" => Maybe(entry.ContactPreference),
            "comment" => Maybe(entry.Comment),
            "durationSeconds" => entry.DurationSeconds > 0 ? [entry.DurationSeconds.ToString(Culture)] : [],
            _ => []
        };

        static IEnumerable<string> Maybe(string? value)
            => string.IsNullOrWhiteSpace(value) ? [] : [value.Trim()];
    }

    private static string? FormatStaffLabel(KioskFeedback entry)
    {
        var name = entry.Staff?.Name?.Trim();
        var branch = entry.BranchName?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            return string.IsNullOrWhiteSpace(branch) ? null : branch;
        }

        if (string.IsNullOrWhiteSpace(branch))
        {
            return name;
        }

        return $"{name} ({branch})";
    }

    private static (string SummaryKind, IReadOnlyList<ValueCountDto> Buckets, double? Average, double? Minimum, double? Maximum, IReadOnlyList<TextAnswerDto> TopText)
        Summarize(LegacyQuestion question, IReadOnlyList<string> rawValues)
    {
        if (question.Type.Equals("number", StringComparison.OrdinalIgnoreCase))
        {
            var numericValues = rawValues
                .Select(v => decimal.TryParse(v, NumberStyles.Number, Culture, out var parsed) ? parsed : (decimal?)null)
                .Where(v => v.HasValue)
                .Select(v => v!.Value)
                .ToList();

            if (numericValues.Count > 0)
            {
                var buckets = numericValues
                    .GroupBy(v => v)
                    .OrderBy(g => g.Key)
                    .Select(g => new ValueCountDto(g.Key.ToString("0.##", Culture), g.Count(), g.Key))
                    .ToList();

                var min = (double)numericValues.Min();
                var max = (double)numericValues.Max();
                var avg = numericValues.Average(v => (double)v);

                return ("scale", buckets, avg, min, max, Array.Empty<TextAnswerDto>());
            }
        }

        if (rawValues.Count > 0)
        {
            var counts = rawValues
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (question.IsMulti || question.Type.Equals("single", StringComparison.OrdinalIgnoreCase))
            {
                var buckets = counts
                    .Select(x => new ValueCountDto(x.Value, x.Count, null))
                    .ToList();

                return ("choice", buckets, null, null, null, Array.Empty<TextAnswerDto>());
            }

            var topText = counts
                .Take(20)
                .Select(x => new TextAnswerDto(x.Value, x.Count))
                .ToList();

            return ("text", Array.Empty<ValueCountDto>(), null, null, null, topText);
        }

        return ("text", Array.Empty<ValueCountDto>(), null, null, null, Array.Empty<TextAnswerDto>());
    }

    private static string FormatNumber(int value) => value.ToString(Culture);

    private static IReadOnlyList<string> ParsePolicies(string? policiesJson)
    {
        if (string.IsNullOrWhiteSpace(policiesJson)) return Array.Empty<string>();
        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<List<string>>(policiesJson);
            return parsed ?? Array.Empty<string>();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
