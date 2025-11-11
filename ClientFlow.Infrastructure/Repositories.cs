using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClientFlow.Application.Abstractions;
using ClientFlow.Domain.Surveys;
using Microsoft.EntityFrameworkCore;

namespace ClientFlow.Infrastructure.Repositories
{
    public sealed class SurveyRepository : ISurveyRepository
    {
        private readonly AppDbContext _db;
        public SurveyRepository(AppDbContext db) => _db = db;

        public Task<List<Survey>> ListAsync(CancellationToken ct)
            => _db.Surveys.AsNoTracking().ToListAsync(ct);

        public Task<List<Survey>> ListForUpdateAsync(CancellationToken ct = default)
            => _db.Surveys.ToListAsync(ct);

        public Task AddAsync(Survey survey, CancellationToken ct)
        {
            _db.Surveys.Add(survey);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
            => _db.SaveChangesAsync(ct);

        public Task<Survey?> GetByCodeAsync(string code, CancellationToken ct = default)
            => _db.Surveys.FirstOrDefaultAsync(x => x.Code == code, ct);

        public Task<Survey?> GetByCodeWithSectionsAndQuestionsAsync(string code, CancellationToken ct = default)
            => _db.Surveys
                  .Include(x => x.Sections)
                  .Include(x => x.Questions)
                  .AsNoTracking()
                  .FirstOrDefaultAsync(x => x.Code == code, ct);

        public Task<Survey?> GetByCodeForUpdateAsync(string code, CancellationToken ct)
           => _db.Surveys.FirstOrDefaultAsync(x => x.Code == code, ct);
    }

    public sealed class OptionRepository : IOptionRepository
    {
        private readonly AppDbContext _db;
        public OptionRepository(AppDbContext db) => _db = db;

        public Task<List<QuestionOption>> GetByQuestionIdsAsync(
            IEnumerable<Guid> qIds, CancellationToken ct = default)
            => _db.Options.AsNoTracking()
                .Where(o => qIds.Contains(o.QuestionId))
                .OrderBy(o => o.Order)
                .ToListAsync(ct);

        public Task AddAsync(QuestionOption option, CancellationToken ct = default)
            => _db.Options.AddAsync(option, ct).AsTask();   // <-- implements the missing method

        public Task<QuestionOption?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Options.FirstOrDefaultAsync(o => o.Id == id, ct);

        public void Remove(QuestionOption option)
            => _db.Options.Remove(option);

        public void RemoveRange(IEnumerable<QuestionOption> options)
            => _db.Options.RemoveRange(options);
    }

    public sealed class RuleRepository : IRuleRepository
    {
        private readonly AppDbContext _db;
        public RuleRepository(AppDbContext db) => _db = db;

        public Task<List<QuestionRule>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default)
            => _db.Rules.AsNoTracking()
                .Where(r => r.SurveyId == surveyId)
                .OrderBy(r => r.Id)
                .ToListAsync(ct);

        public Task AddAsync(QuestionRule rule, CancellationToken ct = default)
            => _db.Rules.AddAsync(rule, ct).AsTask();       // <-- implements the missing method

        public void RemoveRange(IEnumerable<QuestionRule> rules)
            => _db.Rules.RemoveRange(rules);
    }

    public sealed class ResponseRepository : IResponseRepository
    {
        private readonly AppDbContext _db;
        public ResponseRepository(AppDbContext db) => _db = db;

        public Task AddAsync(Response response, CancellationToken ct)
        {
            _db.Responses.Add(response);
            return Task.CompletedTask;
        }

        // Pull NPS numeric answers (assumes Answer.ValueNumber holds the score and Question.Type starts with "nps")
        public Task<List<int>> GetNpsScoresAsync(Guid surveyId, CancellationToken ct)
            => _db.Answers
                .AsNoTracking()
                .Where(a => a.Response.SurveyId == surveyId
                            && a.ValueNumber != null
                            && a.Question.Type.StartsWith("nps"))
                .Select(a => (int)a.ValueNumber!)
                .ToListAsync(ct);
    }

    // Optional: if you prefer a cross-aggregate UoW separate from repo SaveChanges
    public sealed class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _db;
        public UnitOfWork(AppDbContext db) => _db = db;
        public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
    }

    public sealed class SectionRepository : ISectionRepository
    {
        private readonly AppDbContext _db;
        public SectionRepository(AppDbContext db) => _db = db;

        public Task AddAsync(SurveySection section, CancellationToken ct = default)
            => _db.Sections.AddAsync(section, ct).AsTask();

        public Task<SurveySection?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Sections.FirstOrDefaultAsync(s => s.Id == id, ct);

        public Task<List<SurveySection>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default)
            => _db.Sections
                .Where(s => s.SurveyId == surveyId)
                .OrderBy(s => s.Order)
                .ToListAsync(ct);
    }

    public sealed class QuestionRepository : IQuestionRepository
    {
        private readonly AppDbContext _db;
        public QuestionRepository(AppDbContext db) => _db = db;

        public Task AddAsync(Question q, CancellationToken ct = default)
            => _db.Questions.AddAsync(q, ct).AsTask();

        public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => _db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

        public Task<List<Question>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default)
            => _db.Questions
                .Where(q => q.SectionId == sectionId)
                .OrderBy(q => q.Order)
                .ToListAsync(ct);

        public void Remove(Question question)
            => _db.Questions.Remove(question);
    }

    public sealed class SurveyVersionRepository : ISurveyVersionRepository
    {
        private readonly AppDbContext _db;
        public SurveyVersionRepository(AppDbContext db) => _db = db;

        public Task AddAsync(SurveyVersion version, CancellationToken ct = default)
            => _db.SurveyVersions.AddAsync(version, ct).AsTask();

        public Task<List<SurveyVersion>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default)
            => _db.SurveyVersions
                .AsNoTracking()
                .Where(v => v.SurveyId == surveyId)
                .OrderByDescending(v => v.Version)
                .ToListAsync(ct);

        public Task<SurveyVersion?> GetBySurveyAndVersionAsync(Guid surveyId, int version, CancellationToken ct = default)
            => _db.SurveyVersions
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.SurveyId == surveyId && v.Version == version, ct);

        public async Task<int> GetMaxVersionAsync(Guid surveyId, CancellationToken ct = default)
        {
            var existing = await _db.SurveyVersions
                .Where(v => v.SurveyId == surveyId)
                .Select(v => (int?)v.Version)
                .MaxAsync(ct);

            return existing ?? 0;
        }
    }
}
