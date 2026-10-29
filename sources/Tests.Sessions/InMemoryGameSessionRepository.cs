using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Tests.Sessions;

/// <summary>In-memory fake of the session store with the same version check as the EF adapter.</summary>
internal sealed class InMemoryGameSessionRepository : IGameSessionRepository
{
    private readonly Dictionary<Guid, SessionSnapshot> _sessions = [];

    public List<GameActionRecord> Actions { get; } = [];

    public List<SeatResultWrite> Results { get; } = [];

    /// <summary>Simulates another action stored between load and save.</summary>
    public bool FailNextSave { get; set; }

    public void Add(SessionSnapshot session) => _sessions[session.Id] = session;

    public SessionSnapshot Get(Guid sessionId) => _sessions[sessionId];

    public Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? session : null);

    public Task<SessionSnapshot?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var normalized = inviteCode.Trim().ToUpperInvariant();
        var session = _sessions.Values.FirstOrDefault(candidate => candidate.InviteCode == normalized);
        return Task.FromResult(session);
    }

    public Task<SessionSnapshot?> FindOpenSingleplayerAsync(Guid ownerUserId, string gameType, CancellationToken cancellationToken)
    {
        var session = _sessions.Values.FirstOrDefault(candidate =>
            candidate.OwnerUserId == ownerUserId
            && candidate.GameType == gameType
            && candidate.Mode == GameMode.Singleplayer
            && candidate.Status is SessionStatus.Running or SessionStatus.Paused);
        return Task.FromResult(session);
    }

    private readonly Dictionary<Guid, Guid> _rematches = [];

    public Task<SessionSnapshot?> FindRematchAsync(Guid previousSessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_rematches.TryGetValue(previousSessionId, out var id) ? _sessions[id] : null);

    public Task<bool> CreateSessionAsync(SessionCreateWrite write, CancellationToken cancellationToken)
    {
        if (_sessions.ContainsKey(write.Id)
            || (write.PreviousSessionId is { } previous && _rematches.ContainsKey(previous)))
        {
            return Task.FromResult(false);
        }

        if (write.PreviousSessionId is { } previousId)
        {
            _rematches[previousId] = write.Id;
        }

        if (write.InviteCode is not null && _sessions.Values.Any(session => session.InviteCode == write.InviteCode))
        {
            return Task.FromResult(false);
        }

        if (write.Mode == GameMode.Singleplayer
            && _sessions.Values.Any(session =>
                session.OwnerUserId == write.OwnerUserId
                && session.GameType == write.GameType
                && session.Mode == GameMode.Singleplayer
                && session.Status is SessionStatus.Running or SessionStatus.Paused))
        {
            return Task.FromResult(false);
        }

        _sessions[write.Id] = new SessionSnapshot(
            write.Id,
            write.GameType,
            write.Mode,
            write.Status,
            write.OwnerUserId,
            write.InviteCode,
            write.CurrentSeatId,
            write.StateJson,
            write.StateVersion,
            write.RngSeed,
            0,
            [.. write.Seats.OrderBy(seat => seat.Order).Select(seat => new SeatSnapshot(
                seat.Id,
                seat.Order,
                seat.Kind,
                seat.DisplayName,
                seat.Symbol,
                seat.UserId,
                seat.ConnectionStatus,
                seat.AiDifficulty,
                seat.AiStrategyVersion))])
        {
            TurnStartedAt = write.Status == SessionStatus.Running ? write.CreatedAt : null,
        };
        return Task.FromResult(true);
    }

    public Task<bool> AddSeatAsync(SeatAddWrite write, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(write.SessionId, out var session) || session.StateVersion != write.ExpectedVersion)
        {
            return Task.FromResult(false);
        }

        if (session.Seats.Any(seat => seat.Id == write.Seat.Id || seat.Order == write.Seat.Order))
        {
            return Task.FromResult(false);
        }

        _sessions[write.SessionId] = session with
        {
            StateVersion = session.StateVersion + 1,
            Seats =
            [
                .. session.Seats,
                new SeatSnapshot(
                    write.Seat.Id,
                    write.Seat.Order,
                    write.Seat.Kind,
                    write.Seat.DisplayName,
                    write.Seat.Symbol,
                    write.Seat.UserId,
                    write.Seat.ConnectionStatus,
                    write.Seat.AiDifficulty,
                    write.Seat.AiStrategyVersion),
            ],
        };

        return Task.FromResult(true);
    }

    public Task<bool> RemoveSeatAsync(SeatRemoveWrite write, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(write.SessionId, out var session)
            || session.StateVersion != write.ExpectedVersion
            || session.Status != SessionStatus.Lobby
            || session.Seats.All(seat => seat.Id != write.SeatId))
        {
            return Task.FromResult(false);
        }

        var reorders = write.Reorders.ToDictionary(r => r.SeatId);
        _sessions[write.SessionId] = session with
        {
            StateVersion = session.StateVersion + 1,
            Seats =
            [
                .. session.Seats
                    .Where(seat => seat.Id != write.SeatId)
                    .Select(seat => reorders.TryGetValue(seat.Id, out var r) ? seat with { Order = r.Order, Symbol = r.Symbol } : seat)
                    .OrderBy(seat => seat.Order),
            ],
        };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<OpenLobbySummary>> GetOpenLobbiesAsync(string? gameType, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<OpenLobbySummary>>(
        [
            .. _sessions.Values
                .Where(s => s.Mode == GameMode.Multiplayer && s.Status == SessionStatus.Lobby)
                .Where(s => gameType is null || s.GameType == gameType)
                .Take(limit)
                .Select(s => new OpenLobbySummary(
                    s.Id,
                    s.GameType,
                    s.OwnerUserId,
                    s.Seats.FirstOrDefault(seat => seat.UserId == s.OwnerUserId)?.DisplayName ?? string.Empty,
                    s.Seats.Count,
                    DateTimeOffset.UnixEpoch)),
        ]);

    public Task<bool> StartSessionAsync(SessionStartWrite write, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(write.SessionId, out var session)
            || session.StateVersion != write.ExpectedVersion
            || session.Status != SessionStatus.Lobby)
        {
            return Task.FromResult(false);
        }

        _sessions[write.SessionId] = session with
        {
            Status = SessionStatus.Running,
            InviteCode = null,
            CurrentSeatId = write.CurrentSeatId,
            StateJson = write.StateJson,
            StateVersion = session.StateVersion + 1,
            TurnStartedAt = write.UpdatedAt,
        };

        return Task.FromResult(true);
    }

    /// <summary>Disconnect times per seat as written by system actions.</summary>
    public Dictionary<Guid, DateTimeOffset?> DisconnectedAt { get; } = [];

    public Task<IReadOnlyList<Guid>> GetSessionIdsForUserAsync(
        Guid userId,
        IReadOnlyCollection<SessionStatus> statuses,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
        [
            .. _sessions.Values
                .Where(s => statuses.Contains(s.Status) && s.Seats.Any(seat => seat.UserId == userId))
                .Select(s => s.Id),
        ]);

    public Task<IReadOnlyList<PlayerSeatRef>> GetMultiplayerSeatsAsync(SeatConnectionStatus status, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerSeatRef>>(
        [
            .. _sessions.Values
                .Where(s => s.Mode == GameMode.Multiplayer && s.Status == SessionStatus.Running)
                .SelectMany(s => s.Seats
                    .Where(seat => seat.Kind == SeatKind.Human && seat.UserId is not null && seat.ConnectionStatus == status)
                    .Select(seat => new PlayerSeatRef(s.Id, seat.Id, seat.UserId!.Value))),
        ]);

    public Task<IReadOnlyList<PlayerSeatRef>> GetExpiredDisconnectedSeatsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerSeatRef>>(
        [
            .. _sessions.Values
                .Where(s => s.Mode == GameMode.Multiplayer && s.Status == SessionStatus.Running)
                .SelectMany(s => s.Seats
                    .Where(seat => seat.Kind == SeatKind.Human
                        && seat.UserId is not null
                        && seat.ConnectionStatus == SeatConnectionStatus.Disconnected
                        && DisconnectedAt.TryGetValue(seat.Id, out var at) && at is { } time && time <= cutoff)
                    .Select(seat => new PlayerSeatRef(s.Id, seat.Id, seat.UserId!.Value))),
        ]);

    public Task<IReadOnlyList<PlayerSeatRef>> GetTurnTimeoutsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PlayerSeatRef>>(
        [
            .. _sessions.Values
                .Where(s => s.Mode == GameMode.Multiplayer && s.Status == SessionStatus.Running && s.TurnStartedAt is { } at && at <= cutoff)
                .SelectMany(s => s.Seats
                    .Where(seat => seat.Id == s.CurrentSeatId
                        && seat.Kind == SeatKind.Human
                        && seat.UserId is not null
                        && seat.ConnectionStatus == SeatConnectionStatus.Connected)
                    .Select(seat => new PlayerSeatRef(s.Id, seat.Id, seat.UserId!.Value))),
        ]);

    /// <summary>Last activity per session for the retention tests (default: now).</summary>
    public Dictionary<Guid, DateTimeOffset> LastActivity { get; } = [];

    public Task<IReadOnlyList<Guid>> GetInactiveSessionIdsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(
        [
            .. _sessions.Values
                .Where(s => s.Status is SessionStatus.Lobby or SessionStatus.Running or SessionStatus.Paused
                    && LastActivity.TryGetValue(s.Id, out var at) && at <= cutoff)
                .Select(s => s.Id),
        ]);

    public Task<IReadOnlyList<HistoryRecord>> GetHistoryAsync(Guid userId, int skip, int take, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HistoryRecord>>(
        [
            .. _sessions.Values
                .Where(s => s.Status is SessionStatus.Finished or SessionStatus.Aborted && s.Seats.Any(seat => seat.UserId == userId))
                .Skip(skip)
                .Take(take)
                .Select(ToHistory),
        ]);

    public Task<HistoryRecord?> GetHistoryRecordAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? ToHistory(session) : null);

    private HistoryRecord ToHistory(SessionSnapshot session) =>
        new(
            session.Id,
            session.GameType,
            session.Mode,
            session.Status,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            [
                .. session.Seats.Select(seat =>
                {
                    var result = Results.LastOrDefault(r => r.SeatId == seat.Id);
                    return new HistorySeatRecord(seat.Id, seat.Order, seat.DisplayName, seat.Kind, seat.UserId,
                        result?.Outcome, result?.Placement, result?.Score);
                }),
            ]);

    public Task<bool> SetStatusAsync(Guid sessionId, int expectedVersion, SessionStatus status, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || session.StateVersion != expectedVersion)
        {
            return Task.FromResult(false);
        }

        _sessions[sessionId] = session with { Status = status, CurrentSeatId = null, StateVersion = expectedVersion + 1, TurnStartedAt = null };
        return Task.FromResult(true);
    }

    public Task<bool> SaveActionAsync(ActionWrite write, CancellationToken cancellationToken)
    {
        var session = _sessions[write.SessionId];
        if (FailNextSave || session.StateVersion != write.ExpectedVersion)
        {
            FailNextSave = false;
            return Task.FromResult(false);
        }

        Actions.Add(write.Action);
        Results.AddRange(write.Results);
        var changes = (write.SeatChanges ?? []).ToDictionary(change => change.SeatId);
        foreach (var change in changes.Values)
        {
            DisconnectedAt[change.SeatId] = change.DisconnectedAt;
        }

        _sessions[write.SessionId] = session with
        {
            Seats =
            [
                .. session.Seats.Select(seat =>
                    changes.TryGetValue(seat.Id, out var change) ? seat with { ConnectionStatus = change.Status } : seat),
            ],
            CurrentSeatId = write.CurrentSeatId,
            StateJson = write.NewStateJson,
            StateVersion = write.ExpectedVersion + 1,
            Status = write.NewStatus,
            LastSequence = write.Action.Sequence,
            TurnStartedAt = write.NewStatus != SessionStatus.Running ? null : write.TurnStartedAt ?? session.TurnStartedAt,
        };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GameActionRecord>> GetActionsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GameActionRecord>>([.. Actions.Where(a => a.SessionId == sessionId).OrderBy(a => a.Sequence)]);

    public Task<IReadOnlyList<Guid>> GetRunningSessionsWithAiTurnAsync(CancellationToken cancellationToken)
    {
        var result = _sessions.Values
            .Where(session => session.Status == SessionStatus.Running && session.CurrentSeatId is { } currentSeatId
                && session.Seats.Any(seat => seat.Id == currentSeatId && seat.Kind == SeatKind.Ai))
            .Select(session => session.Id)
            .ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(result);
    }
}
