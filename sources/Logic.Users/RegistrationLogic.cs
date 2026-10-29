using System.Net.Mail;
using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>Self-registration with e-mail confirmation (US-005).</summary>
public sealed class RegistrationLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    AccountMailer mailer,
    IAuditLog auditLog) : ALogicBase(appSettings)
{
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
                await mailer.SendConfirmationAsync(
                    email, displayName, created.UserId, created.EmailConfirmationToken, baseUrl, cancellationToken);
                await auditLog.WriteAsync(
                    new AuditRecord(AuditActions.Registered, AuditResult.Success, created.UserId),
                    cancellationToken);
                return new RegistrationOutcome.Accepted();

            case CreateAccountResult.DuplicateEmail:
                // Same answer as for a new address; the owner of the address learns about the attempt by mail.
                await mailer.SendAlreadyRegisteredAsync(email, baseUrl, cancellationToken);
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
}
