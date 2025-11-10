namespace ClientFlow.Domain.Surveys;

/// <summary>
/// Canonical survey question types recognized by the survey designer and renderer.
/// </summary>
public static class QuestionTypes
{
    /// <summary>
    /// Single-line freeform text input.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>placeholder</c> (string?): Optional hint text displayed when the field is empty.</description></item>
    /// <item><description><c>defaultValue</c> (string?): Optional value pre-populated in the input.</description></item>
    /// <item><description><c>maxLength</c> (int?): Maximum number of characters accepted by the renderer.</description></item>
    /// <item><description><c>mask</c> (string?): Optional masking pattern applied client-side.</description></item>
    /// </list>
    /// </remarks>
    public const string Text = "text";

    /// <summary>
    /// Multi-line freeform text input.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>placeholder</c> (string?): Optional hint text displayed when the textarea is empty.</description></item>
    /// <item><description><c>rows</c> (int?): Suggested visible row count.</description></item>
    /// <item><description><c>maxLength</c> (int?): Maximum number of characters accepted by the renderer.</description></item>
    /// </list>
    /// </remarks>
    public const string Textarea = "textarea";

    /// <summary>
    /// Numeric input rendered with native number semantics.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>placeholder</c> (string?): Optional hint text.</description></item>
    /// <item><description><c>min</c> (decimal?): Inclusive minimum value enforced by the renderer.</description></item>
    /// <item><description><c>max</c> (decimal?): Inclusive maximum value enforced by the renderer.</description></item>
    /// <item><description><c>step</c> (decimal?): Increment used for spinner/validation.</description></item>
    /// </list>
    /// </remarks>
    public const string Number = "number";

    /// <summary>
    /// Phone number input with telephone keypad semantics.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>placeholder</c> (string?): Optional hint text.</description></item>
    /// <item><description><c>defaultCountry</c> (string?): ISO country code used for formatting helpers.</description></item>
    /// <item><description><c>format</c> (string?): Optional formatting mask applied client-side.</description></item>
    /// </list>
    /// </remarks>
    public const string Phone = "phone";

    /// <summary>
    /// Email address input with native validation.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>placeholder</c> (string?): Optional hint text.</description></item>
    /// <item><description><c>domainAllowList</c> (string[]?): Domains accepted by the renderer when provided.</description></item>
    /// <item><description><c>autoComplete</c> (string?): Explicit autocomplete attribute value.</description></item>
    /// </list>
    /// </remarks>
    public const string Email = "email";

    /// <summary>
    /// Calendar date picker input.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>min</c> (string?): Minimum allowed ISO date.</description></item>
    /// <item><description><c>max</c> (string?): Maximum allowed ISO date.</description></item>
    /// <item><description><c>displayFormat</c> (string?): Optional formatting hint for custom renderers.</description></item>
    /// </list>
    /// </remarks>
    public const string Date = "date";

    /// <summary>
    /// Time-of-day picker input.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>min</c> (string?): Minimum allowed ISO time.</description></item>
    /// <item><description><c>max</c> (string?): Maximum allowed ISO time.</description></item>
    /// <item><description><c>step</c> (int?): Minute interval supported by the renderer.</description></item>
    /// </list>
    /// </remarks>
    public const string Time = "time";

    /// <summary>
    /// Binary yes/no picker typically rendered as toggle buttons or switches.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>yesLabel</c> (string?): Custom label for the affirmative option.</description></item>
    /// <item><description><c>noLabel</c> (string?): Custom label for the negative option.</description></item>
    /// <item><description><c>layout</c> (string?): Renderer-specific layout hint (e.g. <c>toggle</c>, <c>buttons</c>).</description></item>
    /// </list>
    /// </remarks>
    public const string YesNo = "yesno";

    /// <summary>
    /// Single-select question driven by discrete options.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>presentation</c> (string?): Preferred control type (<c>radio</c>, <c>dropdown</c>, etc.).</description></item>
    /// <item><description><c>orientation</c> (string?): Layout hint such as <c>vertical</c> or <c>horizontal</c>.</description></item>
    /// <item><description><c>otherOption</c> (object?): Configuration for an "other" freeform response (e.g. <c>{ "enabled": true, "label": "Other" }</c>).</description></item>
    /// </list>
    /// </remarks>
    public const string SingleChoice = "single";

    /// <summary>
    /// Multi-select question driven by discrete options.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>presentation</c> (string?): Preferred control type (<c>checkbox</c>, <c>pill</c>, etc.).</description></item>
    /// <item><description><c>maxSelections</c> (int?): Upper bound on selections allowed by the renderer.</description></item>
    /// <item><description><c>otherOption</c> (object?): Configuration for an "other" freeform response.</description></item>
    /// </list>
    /// </remarks>
    public const string MultiChoice = "multi";

    /// <summary>
    /// Likert-scale matrix rendered as a row of rating anchors.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>scale</c> (string[]): Ordered labels representing the Likert anchors.</description></item>
    /// <item><description><c>leftLabel</c> (string?): Helper text describing the negative extreme.</description></item>
    /// <item><description><c>rightLabel</c> (string?): Helper text describing the positive extreme.</description></item>
    /// </list>
    /// </remarks>
    public const string Likert = "likert";

    /// <summary>
    /// Net Promoter Score scale from 0 through 10.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>lowLabel</c> (string?): Helper text displayed near score 0.</description></item>
    /// <item><description><c>highLabel</c> (string?): Helper text displayed near score 10.</description></item>
    /// <item><description><c>colorStops</c> (object[]): Optional color configuration keyed by score ranges.</description></item>
    /// </list>
    /// </remarks>
    public const string NpsZeroToTen = "nps_0_10";

    /// <summary>
    /// Star-based rating control.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>stars</c> (int?): Total number of stars rendered (defaults to five when omitted).</description></item>
    /// <item><description><c>icon</c> (string?): Optional custom icon identifier.</description></item>
    /// <item><description><c>showLabels</c> (bool?): When true, the renderer shows textual captions for each star value.</description></item>
    /// </list>
    /// </remarks>
    public const string RatingStars = "rating_stars";

    /// <summary>
    /// Tabular matrix with rows and columns of inputs.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>rows</c> (object[]): Collection of row definitions containing <c>id</c> and <c>label</c>.</description></item>
    /// <item><description><c>columns</c> (object[]): Collection of column definitions containing <c>id</c>, <c>label</c>, and <c>type</c>.</description></item>
    /// <item><description><c>cellType</c> (string?): Default input type applied when column types are not specified.</description></item>
    /// </list>
    /// </remarks>
    public const string Matrix = "matrix";

    /// <summary>
    /// File upload prompt for collecting attachments.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>allowedTypes</c> (string[]): MIME types accepted by the client.</description></item>
    /// <item><description><c>maxFileSize</c> (int?): Maximum file size in bytes enforced by the renderer.</description></item>
    /// <item><description><c>maxFiles</c> (int?): Maximum number of files the respondent may attach.</description></item>
    /// </list>
    /// </remarks>
    public const string FileUpload = "file";

    /// <summary>
    /// Signature capture field using a drawing surface.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>backgroundColor</c> (string?): Background color of the canvas.</description></item>
    /// <item><description><c>penColor</c> (string?): Stroke color used when collecting the signature.</description></item>
    /// <item><description><c>showGuideline</c> (bool?): Whether a horizontal baseline is displayed.</description></item>
    /// </list>
    /// </remarks>
    public const string Signature = "signature";

    /// <summary>
    /// Static text block rendered as plain paragraph content.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>text</c> (string): Rich text content to display.</description></item>
    /// <item><description><c>alignment</c> (string?): Layout hint (e.g. <c>left</c>, <c>center</c>).</description></item>
    /// <item><description><c>style</c> (object?): Optional typography overrides such as <c>{ "weight": "bold" }</c>.</description></item>
    /// </list>
    /// </remarks>
    public const string StaticText = "static_text";

    /// <summary>
    /// Static HTML block rendered without additional sanitization.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>html</c> (string): Raw HTML fragment rendered directly.</description></item>
    /// <item><description><c>scopedCss</c> (string?): Optional CSS applied to the fragment container.</description></item>
    /// </list>
    /// </remarks>
    public const string StaticHtml = "static_html";

    /// <summary>
    /// Static image block.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>url</c> (string): Source URL for the image asset.</description></item>
    /// <item><description><c>alt</c> (string?): Accessible alternative text.</description></item>
    /// <item><description><c>width</c> (string?): CSS width value applied by the renderer.</description></item>
    /// <item><description><c>height</c> (string?): CSS height value applied by the renderer.</description></item>
    /// </list>
    /// </remarks>
    public const string Image = "image";

    /// <summary>
    /// Static video block supporting embedded players.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>url</c> (string): Source URL for the video asset or embed.</description></item>
    /// <item><description><c>poster</c> (string?): Preview image shown before playback.</description></item>
    /// <item><description><c>autoplay</c> (bool?): Whether playback should start automatically when visible.</description></item>
    /// <item><description><c>loop</c> (bool?): Whether playback should loop.</description></item>
    /// </list>
    /// </remarks>
    public const string Video = "video";

    /// <summary>
    /// Horizontal divider line used to separate sections visually.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>style</c> (string?): Renderer-specific style keyword (e.g. <c>solid</c>, <c>dashed</c>).</description></item>
    /// <item><description><c>color</c> (string?): Optional color override.</description></item>
    /// <item><description><c>thickness</c> (int?): Pixel thickness of the rule.</description></item>
    /// </list>
    /// </remarks>
    public const string Divider = "divider";

    /// <summary>
    /// Vertical spacing block that introduces empty space.
    /// </summary>
    /// <remarks>
    /// Settings JSON contract:
    /// <list type="bullet">
    /// <item><description><c>size</c> (string?): Size token such as <c>sm</c>, <c>md</c>, or <c>lg</c>.</description></item>
    /// <item><description><c>customHeight</c> (int?): Explicit pixel height override.</description></item>
    /// </list>
    /// </remarks>
    public const string Spacer = "spacer";
}
