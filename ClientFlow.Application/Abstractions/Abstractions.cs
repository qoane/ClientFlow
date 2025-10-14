using ClientFlow.Domain.Surveys;

namespace ClientFlow.Application.Abstractions;

public interface ISurveyRepository
{
    Task<List<Survey>> ListAsync(CancellationToken ct);
    Task AddAsync(Survey survey, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<Survey?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<Survey?> GetByCodeWithSectionsAndQuestionsAsync(string code, CancellationToken ct = default);
    Task<Survey?> GetByCodeForUpdateAsync(string code, CancellationToken ct = default);
}

public interface IResponseRepository
{
    Task AddAsync(Response response, CancellationToken ct);
    Task<List<int>> GetNpsScoresAsync(Guid surveyId, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IOptionRepository
{
    Task<List<QuestionOption>> GetByQuestionIdsAsync(IEnumerable<Guid> questionIds, CancellationToken ct = default);
    Task AddAsync(QuestionOption option, CancellationToken ct = default);   // <-- add write method here
}

public interface IRuleRepository
{
    Task<List<QuestionRule>> GetBySurveyIdAsync(Guid surveyId, CancellationToken ct = default);
    Task AddAsync(QuestionRule rule, CancellationToken ct = default);
}

public interface ISectionRepository
{
    Task AddAsync(SurveySection section, CancellationToken ct = default);
}

public interface IQuestionRepository
{
    Task AddAsync(Question question, CancellationToken ct = default);
}
