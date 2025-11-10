// File: ClientFlow.Infrastructure/Repositories.cs
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
}
