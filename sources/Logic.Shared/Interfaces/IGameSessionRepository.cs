using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Shared.Interfaces;

/// <summary>Port for loading sessions and writing applied actions.</summary>
public interface IGameSessionRepository
{
    Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<SessionSnapshot?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken);

    Task<SessionSnapshot?> FindOpenSingleplayerAsync(Guid ownerUserId, string gameType, CancellationToken cancellationToken);

    Task<bool> CreateSessionAsync(SessionCreateWrite write, CancellationToken cancellationToken);

    /// <summary>The new round started from the given session ("Neue Runde"), if any.</summary>
    Task<SessionSnapshot?> FindRematchAsync(Guid previousSessionId, CancellationToken cancellationToken);

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

    /// <summary>Sessions in the given states in which the user has a seat.</summary>
    Task<IReadOnlyList<Guid>> GetSessionIdsForUserAsync(Guid userId, IReadOnlyCollection<SessionStatus> statuses, CancellationToken cancellationToken);

    /// <summary>Human seats of running multiplayer sessions with the given connection status.</summary>
    Task<IReadOnlyList<PlayerSeatRef>> GetMultiplayerSeatsAsync(SeatConnectionStatus status, CancellationToken cancellationToken);

    /// <summary>Disconnected human seats of running multiplayer sessions whose disconnect happened at or before the cutoff.</summary>
    Task<IReadOnlyList<PlayerSeatRef>> GetExpiredDisconnectedSeatsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>
    /// Current seats of running multiplayer sessions that are human, connected and whose turn started at or
    /// before the cutoff (turn time limit).
    /// </summary>
    Task<IReadOnlyList<PlayerSeatRef>> GetTurnTimeoutsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Sets the status (e.g. Aborted) with version check; false on conflict.</summary>
    Task<bool> SetStatusAsync(Guid sessionId, int expectedVersion, SessionStatus status, DateTimeOffset at, CancellationToken cancellationToken);

    /// <summary>Open sessions (Lobby, Running, Paused) without activity since the cutoff.</summary>
    Task<IReadOnlyList<Guid>> GetInactiveSessionIdsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken);

    /// <summary>Finished and aborted sessions of the user, newest first.</summary>
    Task<IReadOnlyList<HistoryRecord>> GetHistoryAsync(Guid userId, int skip, int take, CancellationToken cancellationToken);

    /// <summary>
    /// Admin search over all sessions except lobbies, newest first: <paramref name="query"/> is a session id or part
    /// of a player's display name or e-mail address (empty = all).
    /// </summary>
    Task<IReadOnlyList<HistoryRecord>> SearchGamesAsync(string? query, int skip, int take, CancellationToken cancellationToken);

    /// <summary>History data (seats with results) of one session; null if it does not exist.</summary>
    Task<HistoryRecord?> GetHistoryRecordAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>Running sessions where the current seat is an AI seat.</summary>
    Task<IReadOnlyList<Guid>> GetRunningSessionsWithAiTurnAsync(CancellationToken cancellationToken);
}
