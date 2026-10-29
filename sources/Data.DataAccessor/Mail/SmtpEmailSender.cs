using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;
using Shared.Models.Interfaces;

namespace Data.DataAccessor.Mail;

/// <summary>
/// Sends e-mails via SMTP using App:Email (in development Mailpit, see AppSettings).
/// Without a configured host (only possible outside Live) the mail is skipped with a warning.
/// </summary>
internal sealed class SmtpEmailSender(IAppSettings appSettings, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        var settings = appSettings.Email;
        if (string.IsNullOrWhiteSpace(settings.Host))
        {
            logger.LogWarning("No SMTP host configured (App:Email:Host); e-mail \"{Subject}\" was not sent.", message.Subject);
            return;
        }

        using var mail = new MailMessage
        {
            From = new MailAddress(settings.SenderAddress, settings.SenderName),
            Subject = message.Subject,
            Body = message.TextBody,
            IsBodyHtml = false,
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, null, MediaTypeNames.Text.Html));

        using var client = new SmtpClient(settings.Host, settings.Port)
        {
            EnableSsl = settings.UseTls,
        };

        if (!string.IsNullOrEmpty(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        await client.SendMailAsync(mail, cancellationToken);
    }
}
