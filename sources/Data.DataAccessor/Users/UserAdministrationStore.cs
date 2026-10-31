using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Shared.Models.Account;
using Shared.Models.Administration;

namespace Data.DataAccessor.Users;

/// <summary>Identity-based adapter for <see cref="IUserAdministrationStore"/>.</summary>
internal sealed class UserAdministrationStore(UserManager<User> userManager) : IUserAdministrationStore
{
    public async Task<UserAdminPage> SearchAsync(string? query, bool includeDeleted, int skip, int take, CancellationToken cancellationToken)
    {
        var users = userManager.Users.AsNoTracking();
        if (!includeDeleted)
        {
            users = users.Where(user => user.Status != UserStatus.Deleted);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var pattern = $"%{query.Trim().Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
            users = users.Where(user => EF.Functions.ILike(user.DisplayName, pattern) || EF.Functions.ILike(user.Email!, pattern));
        }

        var total = await users.CountAsync(cancellationToken).ConfigureAwait(false);
        var page = await users
            .OrderBy(user => user.Status)
            .ThenBy(user => user.DisplayName)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = new List<UserAdminRecord>(page.Count);
        foreach (var user in page)
        {
            items.Add(await ToRecordAsync(user).ConfigureAwait(false));
        }

        return new UserAdminPage(items, total);
    }

    public async Task<UserAdminRecord?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        return user is null ? null : await ToRecordAsync(user).ConfigureAwait(false);
    }

    public async Task<bool> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString()).ConfigureAwait(false);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return false;
        }

        user.Status = locked ? UserStatus.Locked : UserStatus.Active;
        if (!locked)
        {
            // Also clear a lockout after failed sign-ins.
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
        }

        var result = await userManager.UpdateAsync(user).ConfigureAwait(false);
        if (result.Succeeded && locked)
        {
            // Existing cookies of the player become invalid with the next security stamp check.
            await userManager.UpdateSecurityStampAsync(user).ConfigureAwait(false);
        }

        return result.Succeeded;
    }

    public async Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken)
    {
        var administrators = await userManager.GetUsersInRoleAsync(RoleNames.Administrator).ConfigureAwait(false);
        return administrators.Count(user => user.Status == UserStatus.Active);
    }

    private async Task<UserAdminRecord> ToRecordAsync(User user) =>
        new(
            user.Id,
            user.DisplayName,
            user.Email ?? string.Empty,
            user.Status switch
            {
                UserStatus.Locked => AccountStatus.Locked,
                UserStatus.Deleted => AccountStatus.Deleted,
                _ => AccountStatus.Active,
            },
            [.. await userManager.GetRolesAsync(user).ConfigureAwait(false)],
            user.EmailConfirmed,
            user.TwoFactorEnabled,
            user.CreatedAt,
            user.LastLoginAt);
}
