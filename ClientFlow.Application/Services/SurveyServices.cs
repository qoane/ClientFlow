using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClientFlow.Application.Abstractions;
using ClientFlow.Application.DTOs;
using ClientFlow.Application.Mapping;
using ClientFlow.Application.Surveys.Definitions;
using ClientFlow.Domain.Surveys;
using System.Text.Json;

namespace ClientFlow.Application.Services;

public class SurveyService
{
    private readonly ISurveyRepository _surveys;
    private readonly IResponseRepository _responses;
    private readonly IOptionRepository _options;
    private readonly IRuleRepository _rules;
    private readonly ISurveyVersionRepository _versions;
    private readonly IUnitOfWork _uow;
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public SurveyService(
        ISurveyRepository surveys,
        IResponseRepository responses,
        IOptionRepository options,
        IRuleRepository rules,
        ISurveyVersionRepository versions,
        IUnitOfWork uow)
        => (_surveys, _responses, _options, _rules, _versions, _uow) = (surveys, responses, options, rules, versions, uow);

    public async Task<SurveyDto?> GetByCodeAsync(string code, CancellationToken ct)
        => (await _surveys.GetByCodeAsync(code, ct))?.ToDto();

    public async Task<Guid?> SubmitAsync(string code, SubmitResponseDto req, CancellationToken ct)
    {
        var s = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (s is null) return null;

        var meta = ExtractResponseMetadata(req.Data);

        var resp = new Response
        {
            Id = Guid.NewGuid(),
            SurveyId = s.Id,
            CreatedUtc = DateTimeOffset.UtcNow,
            Channel = "web",
            StartedUtc = meta.StartedUtc,
            DurationSeconds = meta.DurationSeconds,
            ClientCode = meta.ClientCode,
            FormKey = meta.FormKey
        };

        foreach (var q in s.Questions)
        {
            // only persist an answer if the key exists in the payload
            if (!req.Data.TryGetValue(q.Key, out var raw) || raw is null) continue;

            var v = raw.Trim();
            if (v.Length == 0) continue;

            var a = new Answer
            {
                Id = Guid.NewGuid(),
                ResponseId = resp.Id,
                QuestionId = q.Id
            };

            // Treat any "nps*" question as numeric
            if (q.Type is not null &&
                q.Type.StartsWith("nps", StringComparison.OrdinalIgnoreCase) &&
                decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
            {
                a.ValueNumber = n;
            }
            else
            {
                a.ValueText = v;
            }

            resp.Answers.Add(a);
        }

        // If nothing to save, bail out gracefully
        if (resp.Answers.Count == 0) return null;

        await _responses.AddAsync(resp, ct);
        await _uow.SaveChangesAsync(ct);
        return resp.Id;
    }

    private static (DateTimeOffset? StartedUtc, int? DurationSeconds, string? ClientCode, string? FormKey) ExtractResponseMetadata(
        IReadOnlyDictionary<string, string?> data)
    {
        DateTimeOffset? startedUtc = null;
        if (data.TryGetValue("__startedUtc", out var rawStarted) && !string.IsNullOrWhiteSpace(rawStarted))
        {
            if (DateTimeOffset.TryParse(rawStarted, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                startedUtc = parsed;
            }
        }

        int? durationSeconds = null;
        if (data.TryGetValue("__durationSeconds", out var rawDuration) && int.TryParse(rawDuration, NumberStyles.Integer, CultureInfo.InvariantCulture, out var duration))
        {
            durationSeconds = duration;
        }

        string? ResolveMeta(string key)
            => data.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        return (startedUtc, durationSeconds, ResolveMeta("__clientCode"), ResolveMeta("__formKey"));
    }

    public async Task<NpsSummaryDto?> GetNpsAsync(string code, CancellationToken ct)
    {
        var s = await _surveys.GetByCodeAsync(code, ct);
        if (s is null) return null;

        var scores = await _responses.GetNpsScoresAsync(s.Id, ct);
        int detr = scores.Count(x => x <= 6);
        int pass = scores.Count(x => x is 7 or 8);
        int prom = scores.Count(x => x >= 9);
        int total = scores.Count;
        int score = total == 0 ? 0 : (int)Math.Round(((prom - detr) / (double)total) * 100);

        return new NpsSummaryDto(detr, pass, prom, total, score);
    }

    // Definition for the runner (sections, questions, options, rules)
    public async Task<SurveyDefinitionDto?> GetDefinitionByCodeAsync(string code, CancellationToken ct = default)
    {
        var survey = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (survey is null) return null;

        return await AssembleDefinitionAsync(survey, survey.PublishedVersion ?? 0, ct);
    }

    public async Task<SurveyDefinitionDto?> GetPublishedDefinitionAsync(string code, CancellationToken ct = default)
    {
        var survey = await _surveys.GetByCodeAsync(code, ct);
        if (survey is null) return null;

        if (survey.PublishedVersion is int publishedVersion)
        {
            var snapshot = await _versions.GetBySurveyAndVersionAsync(survey.Id, publishedVersion, ct);
            if (snapshot is not null)
            {
                var fromSnapshot = JsonSerializer.Deserialize<SurveyDefinitionDto>(snapshot.DefinitionJson, SnapshotJsonOptions);
                if (fromSnapshot is not null)
                {
                    return fromSnapshot;
                }
            }
        }

        var tracked = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (tracked is null) return null;

        return await AssembleDefinitionAsync(tracked, survey.PublishedVersion ?? 0, ct);
    }

    public async Task<int?> PublishSurveyAsync(string code, CancellationToken ct = default)
    {
        var survey = await _surveys.GetByCodeWithSectionsAndQuestionsAsync(code, ct);
        if (survey is null) return null;

        var nextVersionSeed = await _versions.GetMaxVersionAsync(survey.Id, ct) + 1;
        var definition = await AssembleDefinitionAsync(survey, nextVersionSeed, ct);
        if (definition is null) return null;

        var snapshot = new SurveyVersion
        {
            Id = Guid.NewGuid(),
            SurveyId = survey.Id,
            Version = nextVersionSeed,
            CreatedUtc = DateTimeOffset.UtcNow,
            DefinitionJson = JsonSerializer.Serialize(definition, SnapshotJsonOptions)
        };

        await _versions.AddAsync(snapshot, ct);

        var surveyForUpdate = await _surveys.GetByCodeForUpdateAsync(code, ct);
        if (surveyForUpdate is null) return null;
        surveyForUpdate.PublishedVersion = nextVersionSeed;

        await _uow.SaveChangesAsync(ct);
        return nextVersionSeed;
    }

    public async Task<IReadOnlyList<SurveyVersionSummaryDto>?> GetSurveyVersionsAsync(string code, CancellationToken ct = default)
    {
        var survey = await _surveys.GetByCodeAsync(code, ct);
        if (survey is null) return null;

        var versions = await _versions.GetBySurveyIdAsync(survey.Id, ct);
        return versions
            .Select(v => new SurveyVersionSummaryDto(v.Version, v.CreatedUtc, survey.PublishedVersion == v.Version))
            .ToArray();
    }

    public async Task<bool> SetPublishedVersionAsync(string code, int version, CancellationToken ct = default)
    {
        var survey = await _surveys.GetByCodeForUpdateAsync(code, ct);
        if (survey is null) return false;

        var exists = await _versions.GetBySurveyAndVersionAsync(survey.Id, version, ct);
        if (exists is null) return false;

        survey.PublishedVersion = version;
        await _uow.SaveChangesAsync(ct);
        return true;
    }

    private async Task<SurveyDefinitionDto?> AssembleDefinitionAsync(Survey survey, int version, CancellationToken ct)
    {
        var questionIds = survey.Questions.Select(q => q.Id).ToList();
        var options = await _options.GetByQuestionIdsAsync(questionIds, ct);
        var rules = await _rules.GetBySurveyIdAsync(survey.Id, ct);

        return SurveyDefinitionMapper.FromEntities(survey, options, rules, version);
    }

}
