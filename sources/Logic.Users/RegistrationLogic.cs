using System.Net;
using System.Net.Mail;
using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>Self-registration with e-mail confirmation (US-005).</summary>
public sealed class RegistrationLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    IEmailSender emailSender,
    IAuditLog auditLog,
    ILogger<RegistrationLogic> logger) : ALogicBase(appSettings)
{
    public const string ConfirmEmailPath = "/api/account/confirm-email";

    public async Task<RegistrationOutcome> RegisterAsync(
        RegisterRequest request,
        string baseUrl,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim() ?? string.Empty;
        var displayName = request.DisplayName?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        var errors = ValidateInput(email, displayName, password);
        if (errors.Count > 0)
        {
            return new RegistrationOutcome.Invalid(errors);
        }

        var result = await accountStore.CreateAsync(email, displayName, password, cancellationToken);

        switch (result)
        {
            case CreateAccountResult.Created created:
                await SendSafelyAsync(
                    BuildConfirmationMail(email, displayName, BuildConfirmationLink(baseUrl, created)),
                    created.UserId,
                    cancellationToken);
                await auditLog.WriteAsync(
                    new AuditRecord(AuditActions.Registered, AuditResult.Success, created.UserId),
                    cancellationToken);
                return new RegistrationOutcome.Accepted();

            case CreateAccountResult.DuplicateEmail:
                // Same answer as for a new address; the owner of the address learns about the attempt by mail.
                await SendSafelyAsync(BuildAlreadyRegisteredMail(email, baseUrl), userId: null, cancellationToken);
                await auditLog.WriteAsync(
                    new AuditRecord(AuditActions.RegistrationWithExistingEmail, AuditResult.Failure),
                    cancellationToken);
                return new RegistrationOutcome.Accepted();

            case CreateAccountResult.InvalidPassword invalid:
                return new RegistrationOutcome.Invalid(new Dictionary<string, string[]>
                {
                    ["password"] = [.. invalid.Errors],
                });

            default:
                throw new InvalidOperationException($"Unknown result {result.GetType().Name}.");
        }
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var confirmed = await accountStore.ConfirmEmailAsync(userId, token, cancellationToken);

        await auditLog.WriteAsync(
            new AuditRecord(AuditActions.EmailConfirmed, confirmed ? AuditResult.Success : AuditResult.Failure, userId),
            cancellationToken);

        return confirmed;
    }

    private static Dictionary<string, string[]> ValidateInput(string email, string displayName, string password)
    {
        var errors = new Dictionary<string, string[]>();

        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            errors["email"] = ["Bitte eine gültige E-Mail-Adresse eingeben."];
        }

        if (displayName.Length is < AccountRules.DisplayNameMinLength or > AccountRules.DisplayNameMaxLength)
        {
            errors["displayName"] =
            [
                $"Der Anzeigename muss {AccountRules.DisplayNameMinLength} bis {AccountRules.DisplayNameMaxLength} Zeichen lang sein.",
            ];
        }

        if (password.Length == 0)
        {
            errors["password"] = ["Bitte ein Passwort eingeben."];
        }

        return errors;
    }

    private static string BuildConfirmationLink(string baseUrl, CreateAccountResult.Created created) =>
        $"{baseUrl.TrimEnd('/')}{ConfirmEmailPath}?userId={created.UserId}&token={Uri.EscapeDataString(created.EmailConfirmationToken)}";

    private static EmailMessage BuildConfirmationMail(string email, string displayName, string link)
    {
        var name = WebUtility.HtmlEncode(displayName);
        var href = WebUtility.HtmlEncode(link);

        return new EmailMessage(
            email,
            "GameHub: Bitte bestätige deine E-Mail-Adresse",
            $"Hallo {displayName},\n\nbitte bestätige deine E-Mail-Adresse, um dein GameHub-Konto zu aktivieren:\n{link}\n\n"
            + "Wenn du dich nicht registriert hast, kannst du diese Nachricht ignorieren.",
            $"<p>Hallo {name},</p><p>bitte bestätige deine E-Mail-Adresse, um dein GameHub-Konto zu aktivieren:</p>"
            + $"<p><a href=\"{href}\">E-Mail-Adresse bestätigen</a></p>"
            + "<p>Wenn du dich nicht registriert hast, kannst du diese Nachricht ignorieren.</p>");
    }

    private static EmailMessage BuildAlreadyRegisteredMail(string email, string baseUrl)
    {
        var url = baseUrl.TrimEnd('/');
        var href = WebUtility.HtmlEncode(url);

        return new EmailMessage(
            email,
            "GameHub: Registrierungsversuch mit deiner E-Mail-Adresse",
            "Hallo,\n\njemand hat versucht, ein GameHub-Konto mit dieser E-Mail-Adresse anzulegen. "
            + $"Es gibt bereits ein Konto dafür. Melde dich einfach an:\n{url}\n\n"
            + "Wenn du das nicht warst, kannst du diese Nachricht ignorieren.",
            "<p>Hallo,</p><p>jemand hat versucht, ein GameHub-Konto mit dieser E-Mail-Adresse anzulegen. "
            + $"Es gibt bereits ein Konto dafür. <a href=\"{href}\">Melde dich einfach an.</a></p>"
            + "<p>Wenn du das nicht warst, kannst du diese Nachricht ignorieren.</p>");
    }

    /// <summary>
    /// A failed mail must not undo or reveal the registration; it is logged and audited.
    /// Resending the confirmation follows with US-006.
    /// </summary>
    private async Task SendSafelyAsync(EmailMessage message, Guid? userId, CancellationToken cancellationToken)
    {
        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Sending the registration e-mail failed.");
            await auditLog.WriteAsync(
                new AuditRecord(AuditActions.ConfirmationEmailFailed, AuditResult.Failure, userId),
                cancellationToken);
        }
    }
}
