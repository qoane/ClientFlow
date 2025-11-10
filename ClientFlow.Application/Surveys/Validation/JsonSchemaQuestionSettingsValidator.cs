using System;
using System.Text.Json;
using ClientFlow.Domain.Surveys;

namespace ClientFlow.Application.Surveys.Validation;

public sealed class JsonSchemaQuestionSettingsValidator : IQuestionSettingsValidator
{
    public QuestionSettingsValidationResult Validate(string type, string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return QuestionSettingsValidationResult.Invalid("Question type is required.");
        }

        var trimmedType = type.Trim();
        JsonDocument? document = null;
        JsonElement root = default;
        var hasSettings = false;

        if (!string.IsNullOrWhiteSpace(settingsJson))
        {
            try
            {
                document = JsonDocument.Parse(settingsJson!, new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip
                });
                root = document.RootElement;
                hasSettings = true;
            }
            catch (JsonException)
            {
                document?.Dispose();
                return QuestionSettingsValidationResult.Invalid("SettingsJson must be valid JSON.");
            }
        }

        try
        {
            if (trimmedType.Equals(QuestionTypes.SingleChoice, StringComparison.OrdinalIgnoreCase) ||
                trimmedType.Equals(QuestionTypes.MultiChoice, StringComparison.OrdinalIgnoreCase))
            {
                if (!hasSettings || root.ValueKind != JsonValueKind.Object)
                {
                    return QuestionSettingsValidationResult.Invalid("Choice questions require a settingsJson object containing a non-empty 'choices' array.");
                }

                if (!TryGetProperty(root, "choices", out var choicesElement) || choicesElement.ValueKind != JsonValueKind.Array)
                {
                    return QuestionSettingsValidationResult.Invalid("Choice questions require a non-empty 'choices' array in settingsJson.");
                }

                var anyChoice = false;
                foreach (var choice in choicesElement.EnumerateArray())
                {
                    if (choice.ValueKind != JsonValueKind.Object)
                    {
                        return QuestionSettingsValidationResult.Invalid("Each choice must be an object containing 'value' and 'label'.");
                    }

                    if (!TryGetProperty(choice, "value", out var valueElement) || valueElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(valueElement.GetString()))
                    {
                        return QuestionSettingsValidationResult.Invalid("Each choice must include a non-empty 'value'.");
                    }

                    if (!TryGetProperty(choice, "label", out var labelElement) || labelElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(labelElement.GetString()))
                    {
                        return QuestionSettingsValidationResult.Invalid("Each choice must include a non-empty 'label'.");
                    }

                    anyChoice = true;
                }

                if (!anyChoice)
                {
                    return QuestionSettingsValidationResult.Invalid("Choice questions require at least one choice.");
                }
            }
            else if (trimmedType.Equals(QuestionTypes.NpsZeroToTen, StringComparison.OrdinalIgnoreCase))
            {
                if (hasSettings && root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetProperty(root, "choices", out var choicesElement) && choicesElement.ValueKind == JsonValueKind.Array && choicesElement.GetArrayLength() > 0)
                    {
                        return QuestionSettingsValidationResult.Invalid("Net Promoter Score questions must not define custom 'choices'.");
                    }

                    if (TryGetProperty(root, "options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array && optionsElement.GetArrayLength() > 0)
                    {
                        return QuestionSettingsValidationResult.Invalid("Net Promoter Score questions must not define custom 'options'.");
                    }
                }
            }
            else if (trimmedType.Equals(QuestionTypes.Matrix, StringComparison.OrdinalIgnoreCase))
            {
                if (!hasSettings || root.ValueKind != JsonValueKind.Object)
                {
                    return QuestionSettingsValidationResult.Invalid("Matrix questions require a settingsJson object containing 'rows' and 'columns' arrays.");
                }

                if (!TryGetProperty(root, "rows", out var rowsElement) || rowsElement.ValueKind != JsonValueKind.Array || rowsElement.GetArrayLength() == 0)
                {
                    return QuestionSettingsValidationResult.Invalid("Matrix questions require a non-empty 'rows' array.");
                }

                if (!TryGetProperty(root, "columns", out var columnsElement) || columnsElement.ValueKind != JsonValueKind.Array || columnsElement.GetArrayLength() == 0)
                {
                    return QuestionSettingsValidationResult.Invalid("Matrix questions require a non-empty 'columns' array.");
                }
            }
            else if (trimmedType.Equals(QuestionTypes.StaticHtml, StringComparison.OrdinalIgnoreCase))
            {
                if (!hasSettings || root.ValueKind != JsonValueKind.Object)
                {
                    return QuestionSettingsValidationResult.Invalid("Static HTML questions require a settingsJson object containing an 'html' string.");
                }

                if (!TryGetProperty(root, "html", out var htmlElement) || htmlElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(htmlElement.GetString()))
                {
                    return QuestionSettingsValidationResult.Invalid("Static HTML questions require a non-empty 'html' string in settingsJson.");
                }
            }
            else if (trimmedType.Equals(QuestionTypes.Image, StringComparison.OrdinalIgnoreCase))
            {
                if (!hasSettings || root.ValueKind != JsonValueKind.Object)
                {
                    return QuestionSettingsValidationResult.Invalid("Image questions require a settingsJson object containing a 'url' string.");
                }

                if (!TryGetProperty(root, "url", out var urlElement) || urlElement.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(urlElement.GetString()))
                {
                    return QuestionSettingsValidationResult.Invalid("Image questions require a non-empty 'url' string in settingsJson.");
                }
            }
        }
        finally
        {
            document?.Dispose();
        }

        return QuestionSettingsValidationResult.Valid();
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
