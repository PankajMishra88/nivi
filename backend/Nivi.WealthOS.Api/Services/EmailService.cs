using System.Net;
using System.Net.Mail;

namespace Nivi.WealthOS.Api.Services;

public class EmailService
{
    private readonly SmtpSettingsService _smtpSettingsService;

    public EmailService(SmtpSettingsService smtpSettingsService)
    {
        _smtpSettingsService = smtpSettingsService;
    }

    public async Task SendAsync(Guid tenantId, string to, string subject, string body)
    {
        var settings = await _smtpSettingsService.GetSettingsAsync(tenantId);
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            return;
        }

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(settings.Username))
        {
            client.Credentials = new NetworkCredential(settings.Username, settings.Password);
        }

        var from = string.IsNullOrWhiteSpace(settings.DefaultFrom)
            ? settings.Username ?? "no-reply@nivi.local"
            : settings.DefaultFrom;

        using var message = new MailMessage(from, to)
        {
            Subject = subject,
            Body = body
        };

        await client.SendMailAsync(message);
    }
}
