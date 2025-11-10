using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClientFlow.Domain.Surveys;

public static class SurveyJsonMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public static SurveyJsonDefinition FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new SurveyJsonValidationException(new[] { "Survey payload cannot be empty." });

        var definition = JsonSerializer.Deserialize<SurveyJsonDefinition>(json, SerializerOptions);
        if (definition is null)
            throw new SurveyJsonValidationException(new[] { "Survey payload is malformed." });

        return definition with
        {
            Sections = definition.Sections ?? [],
            Rules = definition.Rules ?? []
        };
    }

    public static string ToJson(SurveyJsonDefinition definition)
        => JsonSerializer.Serialize(definition, SerializerOptions);

    public static SurveyEntityGraph CreateEntities(SurveyJsonDefinition definition)
    {
        var survey = new Survey
        {
            Sections = [],
            Questions = []
        };

        var options = new List<QuestionOption>();
        var rules = new List<QuestionRule>();

        ApplyToExisting(definition, survey, survey.Sections, survey.Questions, options, rules);
        return new SurveyEntityGraph(survey, survey.Sections, survey.Questions, options, rules);
    }

    public static SurveyEntityGraph ApplyToExisting(
        SurveyJsonDefinition definition,
        Survey survey,
        IList<SurveySection> sections,
        IList<Question> questions,
        IList<QuestionOption> options,
        IList<QuestionRule> rules)
    {
        var expectedId = survey.Id == Guid.Empty ? (Guid?)null : survey.Id;
        Validate(definition, expectedId);

        var surveyId = ResolveGuid(definition.Id) ?? (survey.Id == Guid.Empty ? Guid.NewGuid() : survey.Id);
        if (survey.Id == Guid.Empty)
        {
            survey.Id = surveyId;
        }

        survey.Code = definition.Code.Trim();
        survey.Title = definition.Title.Trim();
        survey.Description = definition.Description;
        survey.ScopedCss = definition.ScopedCss;
        survey.CustomCss = definition.Style?.Css;

        var isPublished = definition.Status?.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(s => string.Equals(s, "Published", StringComparison.OrdinalIgnoreCase)) ?? false;
        survey.IsActive = isPublished;

        ApplyTheme(definition.Theme, survey);

        SyncSections(definition, survey, sections, questions, options, rules);
        SyncRules(definition, survey, questions, rules);

        return new SurveyEntityGraph(survey, sections.ToList(), questions.ToList(), options.ToList(), rules.ToList());
    }

    public static SurveyJsonDefinition ToDefinition(
        Survey survey,
        IEnumerable<SurveySection> sections,
        IEnumerable<Question> questions,
        IEnumerable<QuestionOption> options,
        IEnumerable<QuestionRule> rules)
    {
        var sectionLookup = sections
            .OrderBy(s => s.Order)
            .ThenBy(s => s.Title)
            .ToList();
        var questionLookup = questions
            .OrderBy(q => q.Order)
            .ThenBy(q => q.Key)
            .ToList();
        var optionsByQuestion = options
            .GroupBy(o => o.QuestionId)
            .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Order).ThenBy(o => o.Value).ToList());
        var questionById = questionLookup.ToDictionary(q => q.Id);

        var ruleDtos = rules
            .Select(r => new SurveyRuleJson
            {
                Id = r.Id.ToString(),
                SourceQuestionId = r.SourceQuestionId.ToString(),
                SourceQuestionKey = questionById.TryGetValue(r.SourceQuestionId, out var src) ? src.Key : null,
                Condition = r.Condition,
                Action = r.Action
            })
            .ToList();

        return new SurveyJsonDefinition
        {
            Id = survey.Id.ToString(),
            Code = survey.Code,
            Title = survey.Title,
            Description = survey.Description,
            Theme = ExtractTheme(survey),
            ScopedCss = survey.ScopedCss,
            Style = string.IsNullOrWhiteSpace(survey.CustomCss) ? null : new SurveyStyleJson { Css = survey.CustomCss },
            Sections = sectionLookup.Select(section => new SurveySectionJson
            {
                Id = section.Id.ToString(),
                Title = section.Title,
                Order = section.Order,
                Columns = section.Columns,
                Settings = ParseJsonObject(section.SettingsJson),
                Questions = questionLookup
                    .Where(q => q.SectionId == section.Id)
                    .Select(question => new SurveyQuestionJson
                    {
                        Id = question.Id.ToString(),
                        Key = question.Key,
                        Order = question.Order,
                        Type = question.Type,
                        Prompt = question.Prompt,
                        Required = question.Required,
                        Settings = ParseJsonObject(question.SettingsJson),
                        Options = optionsByQuestion.TryGetValue(question.Id, out var opts)
                            ? opts.Select(o => new SurveyQuestionOptionJson
                                {
                                    Id = o.Id.ToString(),
                                    Value = o.Value,
                                    Label = o.Label,
                                    Order = o.Order
                                })
                                .ToList()
                            : []
                    })
                    .ToList()
            }).ToList(),
            Rules = ruleDtos,
            Version = null,
            Status = survey.IsActive ? "Published" : "Draft"
        };
    }

    private static void Validate(SurveyJsonDefinition definition, Guid? expectedSurveyId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(definition.Code))
            errors.Add("Survey code is required.");
        if (string.IsNullOrWhiteSpace(definition.Title))
            errors.Add("Survey title is required.");

        if (expectedSurveyId.HasValue && ResolveGuid(definition.Id) is Guid providedId && providedId != expectedSurveyId.Value)
            errors.Add($"Survey id mismatch (expected {expectedSurveyId}, found {providedId}).");

        if (definition.Sections is null || definition.Sections.Count == 0)
            errors.Add("At least one section is required.");

        var questionKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var questionIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (definition.Sections is not null)
        {
            for (var i = 0; i < definition.Sections.Count; i++)
            {
                var section = definition.Sections[i];
                if (section is null)
                {
                    errors.Add($"Section at index {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(section.Title))
                    errors.Add($"Section at index {i} must have a title.");

                if (section.Columns <= 0)
                    errors.Add($"Section '{section.Title}' must have at least one column.");

                if (section.Questions is null || section.Questions.Count == 0)
                    errors.Add($"Section '{section.Title}' must contain at least one question.");

                if (section.Questions is not null)
                {
                    for (var j = 0; j < section.Questions.Count; j++)
                    {
                        var question = section.Questions[j];
                        if (question is null)
                        {
                            errors.Add($"Question at index {j} in section '{section.Title}' is null.");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(question.Key))
                            errors.Add($"Question {j + 1} in section '{section.Title}' must have a key.");
                        else if (!questionKeys.Add(question.Key.Trim()))
                            errors.Add($"Duplicate question key '{question.Key}' detected.");

                        if (string.IsNullOrWhiteSpace(question.Type))
                            errors.Add($"Question '{question.Key}' must specify a type.");
                        if (string.IsNullOrWhiteSpace(question.Prompt))
                            errors.Add($"Question '{question.Key}' must specify a prompt.");

                        if (!string.IsNullOrWhiteSpace(question.Id))
                        {
                            questionIds[question.Id.Trim()] = question.Key.Trim();
                        }

                        if (question.Options is not null)
                        {
                            var optionValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            for (var k = 0; k < question.Options.Count; k++)
                            {
                                var option = question.Options[k];
                                if (option is null)
                                {
                                    errors.Add($"Option at index {k} for question '{question.Key}' is null.");
                                    continue;
                                }

                                if (string.IsNullOrWhiteSpace(option.Value))
                                    errors.Add($"Option {k + 1} for question '{question.Key}' must have a value.");
                                else if (!optionValues.Add(option.Value.Trim()))
                                    errors.Add($"Duplicate option value '{option.Value}' for question '{question.Key}'.");

                                if (string.IsNullOrWhiteSpace(option.Label))
                                    errors.Add($"Option value '{option.Value}' for question '{question.Key}' must have a label.");
                            }
                        }
                    }
                }
            }
        }

        if (definition.Rules is not null)
        {
            for (var i = 0; i < definition.Rules.Count; i++)
            {
                var rule = definition.Rules[i];
                if (rule is null)
                {
                    errors.Add($"Rule at index {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(rule.Condition))
                    errors.Add($"Rule {i + 1} must define a condition.");
                if (string.IsNullOrWhiteSpace(rule.Action))
                    errors.Add($"Rule {i + 1} must define an action.");

                var key = DetermineRuleKey(rule);
                if (key is null && !string.IsNullOrWhiteSpace(rule.SourceQuestionId))
                {
                    questionIds.TryGetValue(rule.SourceQuestionId.Trim(), out key);
                }
                if (key is null)
                {
                    errors.Add($"Rule {i + 1} must reference a question via sourceQuestionKey/id or condition prefix.");
                }
                else if (!questionKeys.Contains(key))
                {
                    errors.Add($"Rule {i + 1} references unknown question key '{key}'.");
                }
            }
        }

        if (errors.Count > 0)
            throw new SurveyJsonValidationException(errors);
    }

    private static void SyncSections(
        SurveyJsonDefinition definition,
        Survey survey,
        IList<SurveySection> sections,
        IList<Question> questions,
        IList<QuestionOption> options,
        IList<QuestionRule> rules)
    {
        var sectionById = sections.ToDictionary(x => x.Id);
        var sectionByTitle = sections.ToDictionary(x => x.Title, StringComparer.OrdinalIgnoreCase);
        var retainedSectionIds = new HashSet<Guid>();

        foreach (var sectionJson in (definition.Sections ?? []).OrderBy(s => s.Order).ThenBy(s => s.Title))
        {
            var sectionId = ResolveGuid(sectionJson.Id);
            SurveySection? section = null;
            if (sectionId.HasValue && sectionById.TryGetValue(sectionId.Value, out var existingById))
            {
                section = existingById;
            }
            else if (sectionJson.Id is not null && sectionId.HasValue)
            {
                section = new SurveySection { Id = sectionId.Value, SurveyId = survey.Id };
                sections.Add(section);
                sectionById[section.Id] = section;
            }
            else if (sectionJson.Title is not null && sectionByTitle.TryGetValue(sectionJson.Title, out var existingByTitle))
            {
                section = existingByTitle;
            }
            else
            {
                section = new SurveySection { Id = Guid.NewGuid(), SurveyId = survey.Id };
                sections.Add(section);
                sectionById[section.Id] = section;
            }

            section.SurveyId = survey.Id;
            var previousTitle = section.Title;
            section.Title = string.IsNullOrWhiteSpace(sectionJson.Title) ? "Section" : sectionJson.Title.Trim();
            section.Order = sectionJson.Order;
            section.Columns = sectionJson.Columns <= 0 ? 1 : sectionJson.Columns;
            section.SettingsJson = sectionJson.Settings?.ToJsonString();

            retainedSectionIds.Add(section.Id);
            if (!string.IsNullOrWhiteSpace(previousTitle) && !previousTitle.Equals(section.Title, StringComparison.OrdinalIgnoreCase))
            {
                sectionByTitle.Remove(previousTitle);
            }
            sectionByTitle[section.Title] = section;

            SyncQuestions(section, sectionJson, survey, questions, options, rules);
        }

        var removedSections = sections.Where(s => !retainedSectionIds.Contains(s.Id)).ToList();
        foreach (var removed in removedSections)
        {
            sections.Remove(removed);
            var removedQuestions = questions.Where(q => q.SectionId == removed.Id).ToList();
            foreach (var question in removedQuestions)
            {
                RemoveQuestion(question, questions, options, rules);
            }
        }
    }

    private static void SyncQuestions(
        SurveySection section,
        SurveySectionJson sectionJson,
        Survey survey,
        IList<Question> questions,
        IList<QuestionOption> options,
        IList<QuestionRule> rules)
    {
        var questionsById = questions.ToDictionary(q => q.Id);
        var questionsByKey = questions.ToDictionary(q => q.Key, StringComparer.OrdinalIgnoreCase);
        var retainedQuestionIds = new HashSet<Guid>();

        foreach (var questionJson in (sectionJson.Questions ?? []).OrderBy(q => q.Order).ThenBy(q => q.Key))
        {
            var questionId = ResolveGuid(questionJson.Id);
            Question? question = null;
            if (questionId.HasValue && questionsById.TryGetValue(questionId.Value, out var byId))
            {
                question = byId;
            }
            else if (questionId.HasValue)
            {
                question = new Question { Id = questionId.Value, SurveyId = survey.Id };
                questions.Add(question);
                questionsById[question.Id] = question;
            }
            else if (questionsByKey.TryGetValue(questionJson.Key, out var byKey) && !retainedQuestionIds.Contains(byKey.Id))
            {
                question = byKey;
            }
            else
            {
                question = new Question { Id = Guid.NewGuid(), SurveyId = survey.Id };
                questions.Add(question);
                questionsById[question.Id] = question;
            }

            question.SurveyId = survey.Id;
            question.SectionId = section.Id;
            question.Order = questionJson.Order;
            question.Type = questionJson.Type.Trim();
            question.Prompt = questionJson.Prompt.Trim();
            var previousKey = question.Key;
            question.Key = questionJson.Key.Trim();
            question.Required = questionJson.Required;
            question.SettingsJson = questionJson.Settings?.ToJsonString();

            retainedQuestionIds.Add(question.Id);
            if (!string.IsNullOrWhiteSpace(previousKey) && !previousKey.Equals(question.Key, StringComparison.OrdinalIgnoreCase))
            {
                questionsByKey.Remove(previousKey);
            }
            questionsByKey[question.Key] = question;

            SyncOptions(question, questionJson.Options, options);
        }

        var staleQuestions = questions
            .Where(q => q.SectionId == section.Id && !retainedQuestionIds.Contains(q.Id))
            .ToList();
        foreach (var stale in staleQuestions)
        {
            RemoveQuestion(stale, questions, options, rules);
        }
    }

    private static void SyncOptions(Question question, List<SurveyQuestionOptionJson>? optionJsons, IList<QuestionOption> options)
    {
        optionJsons ??= [];
        var optionsById = options.Where(o => o.QuestionId == question.Id).ToDictionary(o => o.Id);
        var optionsByValue = options.Where(o => o.QuestionId == question.Id).ToDictionary(o => o.Value, StringComparer.OrdinalIgnoreCase);
        var retainedOptionIds = new HashSet<Guid>();

        foreach (var optionJson in optionJsons.OrderBy(o => o.Order).ThenBy(o => o.Value))
        {
            var optionId = ResolveGuid(optionJson.Id);
            QuestionOption? option = null;
            if (optionId.HasValue && optionsById.TryGetValue(optionId.Value, out var byId))
            {
                option = byId;
            }
            else if (optionId.HasValue)
            {
                option = new QuestionOption { Id = optionId.Value, QuestionId = question.Id };
                options.Add(option);
                optionsById[option.Id] = option;
            }
            else if (optionsByValue.TryGetValue(optionJson.Value, out var byValue) && !retainedOptionIds.Contains(byValue.Id))
            {
                option = byValue;
            }
            else
            {
                option = new QuestionOption { Id = Guid.NewGuid(), QuestionId = question.Id };
                options.Add(option);
                optionsById[option.Id] = option;
            }

            option.QuestionId = question.Id;
            var previousValue = option.Value;
            option.Value = optionJson.Value.Trim();
            option.Label = optionJson.Label.Trim();
            option.Order = optionJson.Order;

            retainedOptionIds.Add(option.Id);
            if (!string.IsNullOrWhiteSpace(previousValue) && !previousValue.Equals(option.Value, StringComparison.OrdinalIgnoreCase))
            {
                optionsByValue.Remove(previousValue);
            }
            optionsByValue[option.Value] = option;
        }

        var staleOptions = options
            .Where(o => o.QuestionId == question.Id && !retainedOptionIds.Contains(o.Id))
            .ToList();
        foreach (var stale in staleOptions)
        {
            options.Remove(stale);
        }
    }

    private static void RemoveQuestion(Question question, IList<Question> questions, IList<QuestionOption> options, IList<QuestionRule> rules)
    {
        questions.Remove(question);
        foreach (var option in options.Where(o => o.QuestionId == question.Id).ToList())
        {
            options.Remove(option);
        }

        foreach (var rule in rules.Where(r => r.SourceQuestionId == question.Id).ToList())
        {
            rules.Remove(rule);
        }
    }

    private static void SyncRules(
        SurveyJsonDefinition definition,
        Survey survey,
        IList<Question> questions,
        IList<QuestionRule> rules)
    {
        var questionsById = questions.ToDictionary(q => q.Id);
        var questionsByKey = questions.ToDictionary(q => q.Key, StringComparer.OrdinalIgnoreCase);
        var ruleById = rules.ToDictionary(r => r.Id);
        var retainedRuleIds = new HashSet<Guid>();

        foreach (var ruleJson in definition.Rules.OrderBy(r => r.Condition).ThenBy(r => r.Action))
        {
            var ruleId = ResolveGuid(ruleJson.Id) ?? Guid.NewGuid();
            if (!TryResolveSourceQuestionId(ruleJson, questionsById, questionsByKey, out var sourceQuestionId))
                throw new SurveyJsonValidationException(new[] { $"Rule referencing '{ruleJson.SourceQuestionKey ?? ruleJson.SourceQuestionId ?? ruleJson.Condition}' could not resolve a question." });

            if (!ruleById.TryGetValue(ruleId, out var rule))
            {
                rule = new QuestionRule { Id = ruleId, SurveyId = survey.Id };
                rules.Add(rule);
                ruleById[rule.Id] = rule;
            }

            rule.SurveyId = survey.Id;
            rule.SourceQuestionId = sourceQuestionId;
            rule.Condition = ruleJson.Condition.Trim();
            rule.Action = ruleJson.Action.Trim();

            retainedRuleIds.Add(rule.Id);
        }

        var staleRules = rules.Where(r => !retainedRuleIds.Contains(r.Id)).ToList();
        foreach (var stale in staleRules)
        {
            rules.Remove(stale);
        }
    }

    private static bool TryResolveSourceQuestionId(
        SurveyRuleJson rule,
        IDictionary<Guid, Question> questionsById,
        IDictionary<string, Question> questionsByKey,
        out Guid questionId)
    {
        if (ResolveGuid(rule.SourceQuestionId) is Guid id && questionsById.TryGetValue(id, out var byId))
        {
            questionId = byId.Id;
            return true;
        }

        var key = DetermineRuleKey(rule);
        if (key is not null && questionsByKey.TryGetValue(key, out var byKey))
        {
            questionId = byKey.Id;
            return true;
        }

        questionId = default;
        return false;
    }

    private static string? DetermineRuleKey(SurveyRuleJson rule)
    {
        if (!string.IsNullOrWhiteSpace(rule.SourceQuestionKey))
            return rule.SourceQuestionKey.Trim();

        if (!string.IsNullOrWhiteSpace(rule.SourceQuestionId))
            return null; // id will be handled elsewhere

        if (!string.IsNullOrWhiteSpace(rule.Condition))
        {
            var condition = rule.Condition.Trim();
            var sb = new StringBuilder();
            foreach (var ch in condition)
            {
                if (char.IsLetterOrDigit(ch) || ch is '_' or '-')
                {
                    sb.Append(ch);
                }
                else
                {
                    break;
                }
            }

            var candidate = sb.ToString();
            if (!string.IsNullOrWhiteSpace(candidate))
                return candidate;
        }

        return null;
    }

    private static Guid? ResolveGuid(string? value)
        => Guid.TryParse(value, out var id) && id != Guid.Empty ? id : null;

    private static void ApplyTheme(SurveyThemeJson? theme, Survey survey)
    {
        survey.ThemeAccent = theme?.Accent;
        survey.ThemePanel = theme?.Panel;

        if (theme is null)
        {
            survey.ThemeJson = null;
            return;
        }

        var obj = new JsonObject();
        if (!string.IsNullOrWhiteSpace(theme.Accent)) obj["accent"] = theme.Accent;
        if (!string.IsNullOrWhiteSpace(theme.Panel)) obj["panel"] = theme.Panel;

        survey.ThemeJson = obj.Count > 0 ? obj.ToJsonString() : null;
    }

    private static SurveyThemeJson? ExtractTheme(Survey survey)
    {
        if (string.IsNullOrWhiteSpace(survey.ThemeJson) && survey.ThemeAccent is null && survey.ThemePanel is null)
            return null;

        var accent = survey.ThemeAccent;
        var panel = survey.ThemePanel;

        if (ParseJsonObject(survey.ThemeJson) is JsonObject obj)
        {
            accent ??= obj.TryGetPropertyValue("accent", out var accentNode) ? accentNode?.GetValue<string?>() : null;
            panel ??= obj.TryGetPropertyValue("panel", out var panelNode) ? panelNode?.GetValue<string?>() : null;
        }

        return new SurveyThemeJson { Accent = accent, Panel = panel };
    }

    private static JsonObject? ParseJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }
}
