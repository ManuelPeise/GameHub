using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>
/// Forgot password and resend confirmation (US-006). Requests never reveal whether an address exists:
/// callers always get the same answer, mails go only to existing accounts.
/// </summary>
public sealed class AccountRecoveryLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    AccountMailer mailer,
    IAuditLog auditLog) : ALogicBase(appSettings)
{
    public async Task RequestPasswordResetAsync(string email, string baseUrl, CancellationToken cancellationToken)
    {
        var token = string.IsNullOrWhiteSpace(email)
            ? null
            : await accountStore.CreatePasswordResetTokenAsync(email.Trim(), cancellationToken);

        if (token is not null)
        {
            await mailer.SendPasswordResetAsync(token, baseUrl, cancellationToken);
        }

        await auditLog.WriteAsync(
            new AuditRecord(
                AuditActions.PasswordResetRequested,
                token is null ? AuditResult.Failure : AuditResult.Success,
                token?.UserId),
            cancellationToken);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.UserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Token))
        {
            return new ResetPasswordResult.InvalidToken();
        }

        if (string.IsNullOrEmpty(request.NewPassword))
        {
            return new ResetPasswordResult.InvalidPassword(["Bitte ein neues Passwort eingeben."]);
        }

        var result = await accountStore.ResetPasswordAsync(request.UserId, request.Token, request.NewPassword, cancellationToken);

        if (result is not ResetPasswordResult.InvalidPassword)
        {
            await auditLog.WriteAsync(
                new AuditRecord(
                    AuditActions.PasswordReset,
                    result is ResetPasswordResult.Succeeded ? AuditResult.Success : AuditResult.Failure,
                    request.UserId),
                cancellationToken);
        }

        return result;
    }

    public async Task ResendConfirmationAsync(string email, string baseUrl, CancellationToken cancellationToken)
    {
        var token = string.IsNullOrWhiteSpace(email)
            ? null
            : await accountStore.CreateEmailConfirmationTokenAsync(email.Trim(), cancellationToken);

        if (token is not null)
        {
            await mailer.SendConfirmationAsync(token.Email, token.DisplayName, token.UserId, token.Token, baseUrl, cancellationToken);
        }

        await auditLog.WriteAsync(
            new AuditRecord(
                AuditActions.ConfirmationEmailResent,
                token is null ? AuditResult.Failure : AuditResult.Success,
                token?.UserId),
            cancellationToken);
    }
}
