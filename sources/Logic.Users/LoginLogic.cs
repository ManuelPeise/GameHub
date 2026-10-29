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

        var succeeded = outcome.Result == LoginResult.Succeeded;
        await auditLog.WriteAsync(
            new AuditRecord(
                succeeded ? AuditActions.LoginSucceeded : AuditActions.LoginFailed,
                succeeded ? AuditResult.Success : AuditResult.Failure,
                outcome.UserId,
                Details: succeeded ? null : new Dictionary<string, string> { ["reason"] = outcome.Result.ToString() }),
            cancellationToken);

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
}
