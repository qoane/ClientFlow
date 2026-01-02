using System.Globalization;
using ClientFlow.Domain.Surveys;

namespace ClientFlow.Web.Analytics;

public static class SurveyAnalyticsBuilder
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static SurveyAnalyticsDto Build(
        Survey survey,
        IReadOnlyList<QuestionOption> options,
        IReadOnlyList<Response> responses)
    {
        var sections = survey.Sections
            .GroupBy(s => s.Id)
            .ToDictionary(g => g.Key, g => g.First().Title);

        var questions = survey.Questions
            .OrderBy(q => q.Order)
            .ThenBy(q => q.Prompt)
            .ToList();

        var optionLookup = options
            .GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Order).ToList());

        var answersByQuestion = responses
            .SelectMany(r => r.Answers)
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var responseRows = responses
            .OrderByDescending(r => r.CreatedUtc)
            .Select(r => new ResponseRowDto(
                r.Id,
                r.CreatedUtc,
                r.StartedUtc,
                r.DurationSeconds,
                r.ClientCode,
                r.FormKey,
                BuildAnswerMap(r, questions, optionLookup)))
            .ToList();

        var questionSummaries = new List<QuestionAnalyticsDto>();

        foreach (var question in questions)
        {
            answersByQuestion.TryGetValue(question.Id, out var rawAnswers);
            rawAnswers ??= new List<Answer>();

            optionLookup.TryGetValue(question.Id, out var questionOptions);
            questionOptions ??= new List<QuestionOption>();

            var (summaryKind, buckets, average, minimum, maximum, topText) = SummarizeQuestion(question, rawAnswers, questionOptions);

            var sectionTitle = question.SectionId.HasValue && sections.TryGetValue(question.SectionId.Value, out var s)
                ? s
                : null;

            questionSummaries.Add(new QuestionAnalyticsDto(
                question.Id,
                question.Key,
                question.Prompt,
                sectionTitle,
                question.Type,
                rawAnswers.Count,
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
            responses.Count,
            questionSummaries,
            responseRows);
    }

    private static IReadOnlyDictionary<string, string?> BuildAnswerMap(
        Response response,
        IReadOnlyList<Question> questions,
        IReadOnlyDictionary<Guid, List<QuestionOption>> options)
    {
        var byId = questions.ToDictionary(q => q.Id, q => q, GuidComparer.Instance);
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var answer in response.Answers)
        {
            if (!byId.TryGetValue(answer.QuestionId, out var question))
            {
                continue;
            }

            var key = question.Key;
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            options.TryGetValue(question.Id, out var opts);
            opts ??= new List<QuestionOption>();

            var formatted = FormatAnswerValue(answer, question, opts);
            map[key] = formatted;
        }

        return map;
    }

    private static string? FormatAnswerValue(Answer answer, Question question, IReadOnlyList<QuestionOption> options)
    {
        if (answer.ValueNumber is decimal numeric)
        {
            return numeric.ToString("0.##", Culture);
        }

        var text = (answer.ValueText ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        if (options.Count == 0)
        {
            return text;
        }

        var optionMap = options.ToDictionary(o => o.Value, o => o.Label, StringComparer.OrdinalIgnoreCase);
        if (IsMultiSelect(question.Type))
        {
            var parts = text
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(p => optionMap.TryGetValue(p, out var label) ? label : p)
                .ToArray();
            return parts.Length == 0 ? null : string.Join(", ", parts);
        }

        return optionMap.TryGetValue(text, out var mapped) ? mapped : text;
    }

    private static (string SummaryKind, IReadOnlyList<ValueCountDto> Buckets, double? Average, double? Minimum, double? Maximum, IReadOnlyList<TextAnswerDto> TopText)
        SummarizeQuestion(Question question, IReadOnlyList<Answer> answers, IReadOnlyList<QuestionOption> options)
    {
        var numericValues = answers
            .Where(a => a.ValueNumber is not null)
            .Select(a => (double)a.ValueNumber!.Value)
            .ToList();

        if (numericValues.Count > 0)
        {
            var buckets = numericValues
                .GroupBy(v => v)
                .OrderBy(g => g.Key)
                .Select(g => new ValueCountDto(g.Key.ToString("0.##", Culture), g.Count(), (decimal)g.Key))
                .ToList();

            var min = numericValues.Min();
            var max = numericValues.Max();
            var avg = numericValues.Average();

            return ("scale", buckets, avg, min, max, Array.Empty<TextAnswerDto>());
        }

        var textValues = answers
            .Select(a => (a.ValueText ?? string.Empty).Trim())
            .Where(v => v.Length > 0)
            .ToList();

        if (options.Count > 0)
        {
            var optionMap = options.ToDictionary(o => o.Value, o => o.Label, StringComparer.OrdinalIgnoreCase);
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var text in textValues)
            {
                if (IsMultiSelect(question.Type))
                {
                    foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        var label = optionMap.TryGetValue(part, out var mapped) ? mapped : part;
                        if (label.Length == 0) continue;
                        counts[label] = counts.TryGetValue(label, out var existing) ? existing + 1 : 1;
                    }
                }
                else
                {
                    var label = optionMap.TryGetValue(text, out var mapped) ? mapped : text;
                    if (label.Length == 0) continue;
                    counts[label] = counts.TryGetValue(label, out var existing) ? existing + 1 : 1;
                }
            }

            var buckets = counts
                .OrderByDescending(kvp => kvp.Value)
                .ThenBy(kvp => kvp.Key)
                .Select(kvp => new ValueCountDto(kvp.Key, kvp.Value, null))
                .ToList();

            return ("choice", buckets, null, null, null, Array.Empty<TextAnswerDto>());
        }

        if (textValues.Count > 0)
        {
            var grouped = textValues
                .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TextAnswerDto(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value, StringComparer.OrdinalIgnoreCase)
                .Take(20)
                .ToList();

            return ("text", Array.Empty<ValueCountDto>(), null, null, null, grouped);
        }

        return ("text", Array.Empty<ValueCountDto>(), null, null, null, Array.Empty<TextAnswerDto>());
    }

    private static bool IsMultiSelect(string? type)
        => type != null && type.Contains("multi", StringComparison.OrdinalIgnoreCase);

    private sealed class GuidComparer : IEqualityComparer<Guid>
    {
        public static GuidComparer Instance { get; } = new();
        public bool Equals(Guid x, Guid y) => x == y;
        public int GetHashCode(Guid obj) => obj.GetHashCode();
    }
}
