using ClientFlow.Application.Abstractions;
using ClientFlow.Domain.Surveys;
using Microsoft.EntityFrameworkCore;

namespace ClientFlow.Infrastructure;

public sealed class SurveyRepository : ISurveyRepository
{
    private readonly AppDbContext _db;
    public SurveyRepository(AppDbContext db) => _db = db;

    // Light (no includes) – use where only basic info is needed
    public Task<Survey?> GetByCodeAsync(string code, CancellationToken ct = default)
        => _db.Surveys
              .AsNoTracking()
              .FirstOrDefaultAsync(x => x.Code == code && x.IsActive, ct);

    // Full graph (Sections + Questions) – used by the runner/definition endpoint
    public Task<Survey?> GetByCodeWithSectionsAndQuestionsAsync(string code, CancellationToken ct = default)
    => _db.Surveys
      .Include(x => x.Sections)
     .Include(x => x.Questions)
       .FirstOrDefaultAsync(x => x.Code == code, ct); // tracked

    public Task<Survey?> GetByCodeForUpdateAsync(string code, CancellationToken ct = default) =>
            _db.Surveys                                 // IMPORTANT: no AsNoTracking()
               .FirstOrDefaultAsync(s => s.Code == code && s.IsActive, ct);

    public Task<List<Survey>> ListAsync(CancellationToken ct)
        => _db.Surveys.AsNoTracking().OrderBy(x => x.Title).ToListAsync(ct);

    public async Task AddAsync(Survey survey, CancellationToken ct)
        => await _db.Surveys.AddAsync(survey, ct);

    public Task SaveChangesAsync(CancellationToken ct)
        => _db.SaveChangesAsync(ct);
}

public sealed class ResponseRepository : IResponseRepository
{
    private readonly AppDbContext _db;
    public ResponseRepository(AppDbContext db) => _db = db;

    public Task AddAsync(Response response, CancellationToken ct)
        => _db.Responses.AddAsync(response, ct).AsTask();

    // Pull NPS numeric answers
    public Task<List<int>> GetNpsScoresAsync(Guid surveyId, CancellationToken ct)
        => _db.Answers
            .AsNoTracking()
            .Where(a =>
                   a.ValueNumber != null &&
                   a.Response.SurveyId == surveyId &&                 // use navigation
                   a.Question.Type.StartsWith("nps"))                 // only NPS questions
            .Select(a => (int)a.ValueNumber!)                         // cast to int 0..10
            .ToListAsync(ct);
}

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

public sealed class OptionRepository(AppDbContext db) : IOptionRepository
{
    public Task<List<QuestionOption>> GetByQuestionIdsAsync(IEnumerable<Guid> questionIds, CancellationToken ct = default)
        => db.Options.AsNoTracking()
                     .Where(o => questionIds.Contains(o.QuestionId))
                     .OrderBy(o => o.Order)
                     .ToListAsync(ct);

    public Task AddAsync(QuestionOption option, CancellationToken ct = default)
        => db.Options.AddAsync(option, ct).AsTask();

    public void RemoveRange(IEnumerable<QuestionOption> options)
        => db.Options.RemoveRange(options);
}


public sealed class RuleRepository(AppDbContext db) : IRuleRepository
{
    public Task<List<QuestionRule>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default)
        => db.Rules.AsNoTracking()
                   .Where(r => r.SurveyId == surveyId)
                   .OrderBy(r => r.Id)
                   .ToListAsync(ct);

    public Task AddAsync(QuestionRule rule, CancellationToken ct = default)
        => db.Rules.AddAsync(rule, ct).AsTask();

    public void RemoveRange(IEnumerable<QuestionRule> rules)
        => db.Rules.RemoveRange(rules);
}

public sealed class SectionRepository(AppDbContext db) : ISectionRepository
{
    public Task AddAsync(SurveySection section, CancellationToken ct = default)
        => db.Sections.AddAsync(section, ct).AsTask();

    public Task<SurveySection?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Sections.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<List<SurveySection>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default)
        => db.Sections
            .Where(s => s.SurveyId == surveyId)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);
}

public sealed class QuestionRepository(AppDbContext db) : IQuestionRepository
{
    public Task AddAsync(Question q, CancellationToken ct = default)
        => db.Questions.AddAsync(q, ct).AsTask();

    public Task<Question?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => db.Questions.FirstOrDefaultAsync(q => q.Id == id, ct);

    public Task<List<Question>> GetBySectionIdAsync(Guid sectionId, CancellationToken ct = default)
        => db.Questions
            .Where(q => q.SectionId == sectionId)
            .OrderBy(q => q.Order)
            .ToListAsync(ct);

    public void Remove(Question question)
        => db.Questions.Remove(question);
}
