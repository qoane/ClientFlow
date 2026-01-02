using System.ComponentModel.DataAnnotations;

namespace ClientFlow.Domain.Surveys;

public class Survey
{
    public Guid Id { get; set; }
    [MaxLength(128)]
    public string Code { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public string? ThemeJson { get; set; }   // e.g. { accent: "#de2b2b" }
    public string? ScopedCss { get; set; }   // optional custom CSS

    public int? PublishedVersion { get; set; }

    public List<SurveySection> Sections { get; set; } = [];
    public List<Question> Questions { get; set; } = [];

    [MaxLength(16)] public string? ThemeAccent { get; set; }   // e.g. "#19cba0"
    [MaxLength(16)] public string? ThemePanel { get; set; }   // e.g. "#0f1530"
    public string? CustomCss { get; set; }   // raw CSS from Designer
}

public class SurveySection
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public string Title { get; set; } = "Main";
    public int Order { get; set; }
    public int Columns { get; set; } = 1;
    public string? SettingsJson { get; set; }
}

public class Question
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Guid? SectionId { get; set; }
    public int Order { get; set; }

    public string Type { get; set; } = default!;
    public string Prompt { get; set; } = default!;
    public string Key { get; set; } = default!;
    public bool Required { get; set; }

    public string? SettingsJson { get; set; }

    // <-- add this
    public ICollection<Answer> Answers { get; set; } = new List<Answer>();
}


// Responses
public class Response
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string? Channel { get; set; } // web, sms, kiosk...
    public DateTimeOffset? StartedUtc { get; set; }
    public int? DurationSeconds { get; set; }
    [MaxLength(64)] public string? ClientCode { get; set; }
    [MaxLength(128)] public string? FormKey { get; set; }

    public List<Answer> Answers { get; set; } = [];
}

public class Answer
{
    public Guid Id { get; set; }
    public Guid ResponseId { get; set; }
    public Response Response { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = null!;
    public string? ValueText { get; set; }
    public decimal? ValueNumber { get; set; }
}

public class QuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public string Value { get; set; } = default!;   // stored value, e.g. "promoter"
    public string Label { get; set; } = default!;   // shown to user, e.g. "9 – 10"
    public int Order { get; set; }
}

public class QuestionRule
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Guid SourceQuestionId { get; set; }
    public string Condition { get; set; } = default!;
    // mini DSL, e.g. "equals('nps', 0..6)" or "selected('product','X')"
    public string Action { get; set; } = default!;
    // e.g. "skipTo(section:'Complaints')" or "hide(question:'Comments')"
}

public class ResponseSession
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
    public string Channel { get; set; } = "web"; // kiosk, link, email, etc.
    public string? MetaJson { get; set; }
}

public class SurveyVersion
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public string DefinitionJson { get; set; } = default!;
}
