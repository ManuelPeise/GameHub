using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>Login, logout and the current user (US-006).</summary>
public sealed class LoginLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    IAuditLog auditLog) : ALogicBase(appSettings)
{
    public async Task<LoginOutcome> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var email = request.Email?.Trim() ?? string.Empty;
        if (email.Length == 0 || string.IsNullOrEmpty(request.Password))
        {
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        var outcome = await accountStore.PasswordSignInAsync(email, request.Password, request.RememberMe, cancellationToken);

        // The first step of a two-factor login is not a login yet; it is audited with the second step.
        if (outcome.Result != LoginResult.RequiresTwoFactor)
        {
            await AuditLoginAsync(outcome, "password", cancellationToken);
        }

        return outcome;
    }

    /// <summary>Second step after <see cref="LoginResult.RequiresTwoFactor"/>: code of the authenticator app.</summary>
    public async Task<LoginOutcome> LoginWithTwoFactorAsync(TwoFactorLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outcome = string.IsNullOrWhiteSpace(request.Code)
            ? new LoginOutcome(LoginResult.InvalidCredentials, null)
            : await accountStore.TwoFactorSignInAsync(request.Code, request.RememberMe, cancellationToken);

        await AuditLoginAsync(outcome, "authenticator", cancellationToken);
        return outcome;
    }

    /// <summary>Second step with a recovery code instead of the authenticator app.</summary>
    public async Task<LoginOutcome> LoginWithRecoveryCodeAsync(RecoveryCodeLoginRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var outcome = string.IsNullOrWhiteSpace(request.RecoveryCode)
            ? new LoginOutcome(LoginResult.InvalidCredentials, null)
            : await accountStore.RecoveryCodeSignInAsync(request.RecoveryCode, cancellationToken);

        await AuditLoginAsync(outcome, "recoveryCode", cancellationToken);
        return outcome;
    }

    public async Task LogoutAsync(Guid? userId, CancellationToken cancellationToken)
    {
        await accountStore.SignOutAsync(cancellationToken);

        if (userId is not null)
        {
            await auditLog.WriteAsync(new AuditRecord(AuditActions.Logout, AuditResult.Success, userId), cancellationToken);
        }
    }

    public Task<AccountInfo?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken) =>
        accountStore.FindAsync(userId, cancellationToken);

    private Task AuditLoginAsync(LoginOutcome outcome, string method, CancellationToken cancellationToken)
    {
        var succeeded = outcome.Result == LoginResult.Succeeded;
        var details = new Dictionary<string, string> { ["method"] = method };
        if (!succeeded)
        {
            details["reason"] = outcome.Result.ToString();
        }

        return auditLog.WriteAsync(
            new AuditRecord(
                succeeded ? AuditActions.LoginSucceeded : AuditActions.LoginFailed,
                succeeded ? AuditResult.Success : AuditResult.Failure,
                outcome.UserId,
                Details: details),
            cancellationToken);
    }
}
