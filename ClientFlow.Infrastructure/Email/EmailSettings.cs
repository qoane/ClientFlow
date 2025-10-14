namespace ClientFlow.Infrastructure.Email;

/// <summary>
/// Holds SMTP configuration used for sending email notifications.
/// Values are read from appsettings.json via the options pattern.
/// </summary>
public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 25;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = string.Empty;
}