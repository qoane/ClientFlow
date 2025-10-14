namespace ClientFlow.Application.Services;

/// <summary>
/// Configuration settings for JSON Web Token generation and validation.  These values are
/// loaded from appsettings.json and injected via IOptions&lt;JwtSettings&gt;.  You can adjust
/// the secret key, issuer, audience and expiry as needed to suit your environment.
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// A random, secret key used to sign JWTs.  This should be at least 32 characters
    /// long and kept secure.  Do not expose it publicly.  When changing this value all
    /// existing tokens become invalid.
    /// </summary>
    public string SecretKey { get; set; } = string.Empty;
    /// <summary>
    /// The issuer (iss) claim placed in generated tokens.  Typically the name of your
    /// application or organisation.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;
    /// <summary>
    /// The expected audience (aud) for generated tokens.  Clients should verify this
    /// matches their own identifier when validating tokens.
    /// </summary>
    public string Audience { get; set; } = string.Empty;
    /// <summary>
    /// The number of minutes for which generated JWTs remain valid.  After expiry
    /// the client must request a new token.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;
}