namespace Logic.Shared.Interfaces;

/// <summary>
/// Port used by the account deletion: ends the participation of a user in all open sessions
/// (running multiplayer: removed like after a lost connection; singleplayer and own lobbies: aborted).
/// </summary>
public interface IPlayerSessionCleanup
{
    Task RemoveUserFromOpenSessionsAsync(Guid userId, CancellationToken cancellationToken);
}
