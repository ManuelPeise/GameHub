using Logic.Shared;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Administration;
using Shared.Models.Interfaces;

namespace Logic.Users;

public enum AdminActionResult
{
    Done,
    NotFound,

    /// <summary>Administrators cannot lock or delete their own account here.</summary>
    Self,

    /// <summary>The action would leave no active administrator.</summary>
    LastAdministrator,

    NotConfirmed,

    /// <summary>E.g. unlocking an account that is not locked, or changing a deleted account.</summary>
    InvalidState,
}

/// <summary>Player administration for administrators: search, lock, unlock and delete (US-025).</summary>
public sealed class PlayerAdministrationLogic(
    IAppSettings appSettings,
    IUserAdministrationStore store,
    AccountDeletionLogic deletion,
    IAuditLog auditLog) : ALogicBase(appSettings)
{
    public const int MaxPageSize = 50;

    public async Task<AdminUserPageDto> SearchAsync(
        Guid adminId,
        string? query,
        bool includeDeleted,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var page = await store
            .SearchAsync(query, includeDeleted, Math.Max(0, skip), Math.Clamp(take, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return new AdminUserPageDto([.. page.Items.Select(user => ToDto(user, adminId))], page.Total);
    }

    public Task<AdminActionResult> LockAsync(Guid adminId, Guid userId, CancellationToken cancellationToken) =>
        SetLockedAsync(adminId, userId, locked: true, cancellationToken);

    public Task<AdminActionResult> UnlockAsync(Guid adminId, Guid userId, CancellationToken cancellationToken) =>
        SetLockedAsync(adminId, userId, locked: false, cancellationToken);

    /// <summary>Deletes a player with the same pseudonymization as the self-deletion (US-008).</summary>
    public async Task<AdminActionResult> DeleteAsync(Guid adminId, Guid userId, bool confirmed, CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            return AdminActionResult.NotConfirmed;
        }

        var check = await CheckAsync(adminId, userId, cancellationToken).ConfigureAwait(false);
        if (check.Result != AdminActionResult.Done)
        {
            await AuditAsync(AuditActions.AccountDeleted, AuditResult.Failure, adminId, userId, check.Result, cancellationToken).ConfigureAwait(false);
            return check.Result;
        }

        // Writes the audit entry itself (by = admin) and ends the player's open games.
        var result = await deletion.DeleteAsync(userId, deletedBy: "admin", cancellationToken).ConfigureAwait(false);
        return result == AccountDeletionResult.Deleted ? AdminActionResult.Done : AdminActionResult.NotFound;
    }

    private async Task<AdminActionResult> SetLockedAsync(Guid adminId, Guid userId, bool locked, CancellationToken cancellationToken)
    {
        var action = locked ? AuditActions.AccountLocked : AuditActions.AccountUnlocked;
        var (result, user) = await CheckAsync(adminId, userId, cancellationToken, checkLastAdministrator: locked).ConfigureAwait(false);
        if (result == AdminActionResult.Done && user!.Status != (locked ? AccountStatus.Active : AccountStatus.Locked))
        {
            result = AdminActionResult.InvalidState;
        }

        if (result == AdminActionResult.Done && !await store.SetLockedAsync(userId, locked, cancellationToken).ConfigureAwait(false))
        {
            result = AdminActionResult.NotFound;
        }

        await AuditAsync(action, result == AdminActionResult.Done ? AuditResult.Success : AuditResult.Failure, adminId, userId, result, cancellationToken)
            .ConfigureAwait(false);
        return result;
    }

    /// <summary>Common checks: account exists and is not deleted, not the own account, not the last administrator.</summary>
    private async Task<(AdminActionResult Result, UserAdminRecord? User)> CheckAsync(
        Guid adminId,
        Guid userId,
        CancellationToken cancellationToken,
        bool checkLastAdministrator = true)
    {
        var user = await store.FindAsync(userId, cancellationToken).ConfigureAwait(false);
        if (user is null || user.Status == AccountStatus.Deleted)
        {
            return (AdminActionResult.NotFound, user);
        }

        if (userId == adminId)
        {
            return (AdminActionResult.Self, user);
        }

        if (checkLastAdministrator
            && user.Roles.Contains(RoleNames.Administrator)
            && user.Status == AccountStatus.Active
            && await store.CountActiveAdministratorsAsync(cancellationToken).ConfigureAwait(false) <= 1)
        {
            return (AdminActionResult.LastAdministrator, user);
        }

        return (AdminActionResult.Done, user);
    }

    private Task AuditAsync(string action, AuditResult result, Guid adminId, Guid userId, AdminActionResult outcome, CancellationToken cancellationToken) =>
        auditLog.WriteAsync(
            new AuditRecord(
                action,
                result,
                adminId,
                Target: userId.ToString(),
                Details: new Dictionary<string, string> { ["by"] = "admin", ["outcome"] = outcome.ToString() }),
            cancellationToken);

    private static AdminUserDto ToDto(UserAdminRecord user, Guid adminId) =>
        new(
            user.Id,
            user.DisplayName,
            user.Email,
            user.Status,
            user.Roles,
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            user.CreatedAt,
            user.LastLoginAt,
            user.Id == adminId);
}
