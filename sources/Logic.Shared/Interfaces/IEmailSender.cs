using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for outgoing e-mails (SMTP; Mailpit in Development and Testing).</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
