using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Linq;

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
        if (!MailAddress.TryCreate(_settings.From, out var fromAddress))
        {
            return;
        }
        var validRecipients = recipients
            .Where(static r => !string.IsNullOrWhiteSpace(r))
            .Select(static r => r.Trim())
            .Where(static r => MailAddress.TryCreate(r, out _))
            .ToArray();
        if (validRecipients.Length == 0)
        {
            return;
        }
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
            From = fromAddress,
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true
        };
        foreach (var rec in validRecipients)
        {
            mail.To.Add(rec);
        }
        await client.SendMailAsync(mail, ct);
    }
}