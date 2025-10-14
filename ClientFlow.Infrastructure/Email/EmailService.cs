using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;

namespace ClientFlow.Infrastructure.Email;

/// <summary>
/// Provides functionality to send emails via SMTP using the configured
/// <see cref="EmailSettings"/>.  You must configure the host,
/// port, from address and optionally credentials in appsettings.json.
/// </summary>
public class EmailService
{
    private readonly EmailSettings _settings;
    public EmailService(IOptions<EmailSettings> settings) => _settings = settings.Value;

    public async Task SendAsync(string[] recipients, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (recipients.Length == 0) return;
        using var client = new SmtpClient(_settings.Host, _settings.Port)
        {
            EnableSsl = true
        };
        if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
        {
            client.Credentials = new NetworkCredential(_settings.Username, _settings.Password);
        }
        var mail = new MailMessage
        {
            From = new MailAddress(_settings.From),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        foreach (var rec in recipients)
        {
            if (!string.IsNullOrWhiteSpace(rec)) mail.To.Add(rec);
        }
        await client.SendMailAsync(mail, ct);
    }
}