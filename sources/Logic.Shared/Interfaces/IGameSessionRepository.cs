using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for loading sessions and writing applied actions.</summary>
public interface IGameSessionRepository
{
    Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<SessionSnapshot?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<SessionSnapshot?> FindOpenSingleplayerAsync(Guid ownerUserId, string gameType, CancellationToken cancellationToken);

    Task<bool> CreateSessionAsync(SessionCreateWrite write, CancellationToken cancellationToken);

    Task<bool> AddSeatAsync(SeatAddWrite write, CancellationToken cancellationToken);

    /// <summary>Removes a lobby seat and moves the following seats up. False on version conflict.</summary>
    Task<bool> RemoveSeatAsync(SeatRemoveWrite write, CancellationToken cancellationToken);

    /// <summary>Multiplayer sessions in status Lobby, newest first.</summary>
    Task<IReadOnlyList<OpenLobbySummary>> GetOpenLobbiesAsync(string? gameType, int limit, CancellationToken cancellationToken);

    Task<bool> StartSessionAsync(SessionStartWrite write, CancellationToken cancellationToken);

    /// <summary>
    /// Appends the action to the move log and stores the new state with version + 1.
    /// Returns false (and writes nothing) if another action was stored in the meantime.
    /// </summary>
    Task<bool> SaveActionAsync(ActionWrite write, CancellationToken cancellationToken);

    /// <summary>The move log of a session, ordered by sequence.</summary>
    Task<IReadOnlyList<GameActionRecord>> GetActionsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Running sessions where the current seat is an AI seat.</summary>
    Task<IReadOnlyList<Guid>> GetRunningSessionsWithAiTurnAsync(CancellationToken cancellationToken);
}
