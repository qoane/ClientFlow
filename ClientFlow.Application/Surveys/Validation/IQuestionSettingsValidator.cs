using System;

namespace ClientFlow.Application.Surveys.Validation;

public interface IQuestionSettingsValidator
{
    QuestionSettingsValidationResult Validate(string type, string? settingsJson);
}

public readonly record struct QuestionSettingsValidationResult(bool IsValid, string? ErrorMessage)
{
    public static QuestionSettingsValidationResult Valid() => new(true, null);

    public static QuestionSettingsValidationResult Invalid(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Validation error message must be provided.", nameof(message));
        }

        return new QuestionSettingsValidationResult(false, message);
    }
}
