using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for loading sessions and writing applied actions.</summary>
public interface IGameSessionRepository
{
    Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the action to the move log and stores the new state with version + 1.
    /// Returns false (and writes nothing) if another action was stored in the meantime.
    /// </summary>
    Task<bool> SaveActionAsync(ActionWrite write, CancellationToken cancellationToken);

    /// <summary>The move log of a session, ordered by sequence.</summary>
    Task<IReadOnlyList<GameActionRecord>> GetActionsAsync(Guid sessionId, CancellationToken cancellationToken);
}
