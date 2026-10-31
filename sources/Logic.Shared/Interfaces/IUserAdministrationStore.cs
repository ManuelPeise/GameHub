using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for the player administration (list, lock, unlock).</summary>
public interface IUserAdministrationStore
{
    /// <param name="query">Part of the display name or e-mail address; empty = all.</param>
    /// <param name="includeDeleted">Also list pseudonymized accounts.</param>
    Task<UserAdminPage> SearchAsync(string? query, bool includeDeleted, int skip, int take, CancellationToken cancellationToken);

    Task<UserAdminRecord?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks or unlocks an account. Locking also renews the security stamp, so existing sign-ins of the
    /// player end with the next session check. Returns false if the account does not exist or is deleted.
    /// </summary>
    Task<bool> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken);

    /// <summary>Administrators that are neither locked nor deleted.</summary>
    Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken);
}
