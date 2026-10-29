using System.Net;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;

namespace Logic.Users;

/// <summary>Builds and sends the account e-mails (German). A failed mail is logged and audited, never thrown.</summary>
public sealed class AccountMailer(IEmailSender emailSender, IAuditLog auditLog, ILogger<AccountMailer> logger)
{
    public const string ConfirmEmailPath = "/api/account/confirm-email";
    public const string ResetPasswordPath = "/reset-password";

    public Task SendConfirmationAsync(
        string email,
        string displayName,
        Guid userId,
        string token,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        var link = BuildLink(baseUrl, ConfirmEmailPath, userId, token);

        return SendSafelyAsync(
            BuildMail(
                email,
                "GameHub: Bitte bestätige deine E-Mail-Adresse",
                $"Hallo {displayName},",
                "bitte bestätige deine E-Mail-Adresse, um dein GameHub-Konto zu aktivieren:",
                link,
                "E-Mail-Adresse bestätigen",
                "Wenn du dich nicht registriert hast, kannst du diese Nachricht ignorieren."),
            userId,
            cancellationToken);
    }

    public Task SendAlreadyRegisteredAsync(string email, string baseUrl, CancellationToken cancellationToken) =>
        SendSafelyAsync(
            BuildMail(
                email,
                "GameHub: Registrierungsversuch mit deiner E-Mail-Adresse",
                "Hallo,",
                "jemand hat versucht, ein GameHub-Konto mit dieser E-Mail-Adresse anzulegen. Es gibt bereits ein Konto dafür. "
                + "Melde dich einfach an; falls du dein Passwort vergessen hast, kannst du es dort zurücksetzen:",
                $"{baseUrl.TrimEnd('/')}/login",
                "Zur Anmeldung",
                "Wenn du das nicht warst, kannst du diese Nachricht ignorieren."),
            userId: null,
            cancellationToken);

    public Task SendPasswordResetAsync(AccountToken token, string baseUrl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);

        return SendSafelyAsync(
            BuildMail(
                token.Email,
                "GameHub: Passwort zurücksetzen",
                $"Hallo {token.DisplayName},",
                "über den folgenden Link kannst du ein neues Passwort für dein GameHub-Konto festlegen. Der Link ist nur begrenzt gültig.",
                BuildLink(baseUrl, ResetPasswordPath, token.UserId, token.Token),
                "Neues Passwort festlegen",
                "Wenn du das nicht angefordert hast, kannst du diese Nachricht ignorieren; dein Passwort bleibt unverändert."),
            token.UserId,
            cancellationToken);
    }

    internal static string BuildLink(string baseUrl, string path, Guid userId, string token) =>
        $"{baseUrl.TrimEnd('/')}{path}?userId={userId}&token={Uri.EscapeDataString(token)}";

    private static EmailMessage BuildMail(
        string to,
        string subject,
        string greeting,
        string text,
        string link,
        string linkText,
        string footer)
    {
        var html =
            $"<p>{WebUtility.HtmlEncode(greeting)}</p>"
            + $"<p>{WebUtility.HtmlEncode(text)}</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(linkText)}</a></p>"
            + $"<p>{WebUtility.HtmlEncode(footer)}</p>";

        return new EmailMessage(to, subject, $"{greeting}\n\n{text}\n{link}\n\n{footer}", html);
    }

    private async Task SendSafelyAsync(EmailMessage message, Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Sending the account e-mail \"{Subject}\" failed.", message.Subject);
            await auditLog.WriteAsync(
                new AuditRecord(
                    AuditActions.EmailFailed,
                    AuditResult.Failure,
                    userId,
                    Details: new Dictionary<string, string> { ["subject"] = message.Subject }),
                cancellationToken);
        }
    }
}
