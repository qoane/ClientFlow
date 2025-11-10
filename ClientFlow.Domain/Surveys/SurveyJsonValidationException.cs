namespace ClientFlow.Domain.Surveys;

public sealed class SurveyJsonValidationException : Exception
{
    public SurveyJsonValidationException(IEnumerable<string> errors)
        : base($"Survey definition is invalid: {string.Join(", ", errors)}")
    {
        Errors = errors.ToArray();
    }

    public IReadOnlyList<string> Errors { get; }
}
