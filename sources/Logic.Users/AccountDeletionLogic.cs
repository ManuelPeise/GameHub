using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Logic.Users;

/// <summary>Result of an account deletion request.</summary>
public enum AccountDeletionResult
{
    Deleted,
    NotConfirmed,
    WrongPassword,
    NotFound,
}

/// <summary>
/// Deleting an account by pseudonymization (US-008; reused by administrators in US-025). Personal data
/// is removed, the account row and the game history stay. Running games of the user are ended with the
/// rules of a removed player (US-014).
/// </summary>
public sealed class AccountDeletionLogic(
    IAppSettings appSettings,
    IUserAccountStore accountStore,
    IAuditLog auditLog,
    IPlayerSessionCleanup? sessionCleanup = null) : ALogicBase(appSettings)
{
    /// <summary>The signed-in user deletes the own account: requires the current password and a confirmation.</summary>
    public async Task<AccountDeletionResult> DeleteOwnAccountAsync(
        Guid userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Confirmed)
        {
            return AccountDeletionResult.NotConfirmed;
        }

        if (string.IsNullOrEmpty(request.Password)
            || !await accountStore.CheckPasswordAsync(userId, request.Password, cancellationToken))
        {
            await auditLog.WriteAsync(
                new AuditRecord(AuditActions.AccountDeleted, AuditResult.Failure, userId,
                    Details: new Dictionary<string, string> { ["reason"] = "WrongPassword", ["by"] = "self" }),
                cancellationToken);
            return AccountDeletionResult.WrongPassword;
        }

        var result = await DeleteAsync(userId, deletedBy: "self", cancellationToken);
        if (result == AccountDeletionResult.Deleted)
        {
            await accountStore.SignOutAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Pseudonymizes the account and writes the audit entry; shared by self-deletion and the admin area.
    /// </summary>
    public async Task<AccountDeletionResult> DeleteAsync(Guid userId, string deletedBy, CancellationToken cancellationToken)
    {
        // Running games first: other players are informed and the game goes on (or ends) without this user.
        if (sessionCleanup is not null)
        {
            await sessionCleanup.RemoveUserFromOpenSessionsAsync(userId, cancellationToken);
        }

        var deleted = await accountStore.PseudonymizeAsync(userId, cancellationToken);

        await auditLog.WriteAsync(
            new AuditRecord(
                AuditActions.AccountDeleted,
                deleted ? AuditResult.Success : AuditResult.Failure,
                userId,
                Details: new Dictionary<string, string> { ["by"] = deletedBy }),
            cancellationToken);

        return deleted ? AccountDeletionResult.Deleted : AccountDeletionResult.NotFound;
    }
}
