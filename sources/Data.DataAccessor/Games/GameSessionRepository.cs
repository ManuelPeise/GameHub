using Data.Database;
using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Models.Games;

namespace Data.DataAccessor.Games;

/// <summary>EF Core adapter for <see cref="IGameSessionRepository"/>.</summary>
public sealed class GameSessionRepository(GameDbContext database) : IGameSessionRepository
{
    public async Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await database.GameSessions.AsNoTracking()
            .Include(s => s.Seats)
            .FirstOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            .ConfigureAwait(false);
        return session is null ? null : await ToSnapshotAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionSnapshot?> FindByInviteCodeAsync(string inviteCode, CancellationToken cancellationToken)
    {
        var normalized = inviteCode.Trim().ToUpperInvariant();
        var session = await database.GameSessions.AsNoTracking()
            .Include(s => s.Seats)
            .FirstOrDefaultAsync(s => s.InviteCode == normalized, cancellationToken)
            .ConfigureAwait(false);
        return session is null ? null : await ToSnapshotAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task<SessionSnapshot?> FindOpenSingleplayerAsync(Guid ownerUserId, string gameType, CancellationToken cancellationToken)
    {
        var session = await database.GameSessions.AsNoTracking()
            .Include(s => s.Seats)
            .FirstOrDefaultAsync(
                s => s.OwnerUserId == ownerUserId
                    && s.GameType == gameType
                    && s.Mode == GameMode.Singleplayer
                    && (s.Status == SessionStatus.Running || s.Status == SessionStatus.Paused),
                cancellationToken)
            .ConfigureAwait(false);
        return session is null ? null : await ToSnapshotAsync(session, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> CreateSessionAsync(SessionCreateWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        database.GameSessions.Add(new GameSession
        {
            Id = write.Id,
            GameType = write.GameType,
            Mode = write.Mode,
            Status = write.Status,
            OwnerUserId = write.OwnerUserId,
            InviteCode = write.InviteCode,
            CurrentSeatId = write.CurrentSeatId,
            StateJson = write.StateJson,
            StateVersion = write.StateVersion,
            RngSeed = write.RngSeed,
            CreatedAt = write.CreatedAt,
            UpdatedAt = write.CreatedAt,
            LastActivityAt = write.CreatedAt,
            TurnStartedAt = write.Status == SessionStatus.Running ? write.CreatedAt : null,
        });

        foreach (var seat in write.Seats)
        {
            database.Seats.Add(new Seat
            {
                Id = seat.Id,
                SessionId = write.Id,
                Order = seat.Order,
                Kind = seat.Kind,
                DisplayName = seat.DisplayName,
                Symbol = seat.Symbol,
                UserId = seat.UserId,
                AiDifficulty = seat.AiDifficulty,
                AiStrategyVersion = seat.AiStrategyVersion,
                ConnectionStatus = seat.ConnectionStatus,
                DisconnectedAt = seat.DisconnectedAt,
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> AddSeatAsync(SeatAddWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        var session = await database.GameSessions
            .FirstOrDefaultAsync(s => s.Id == write.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.StateVersion != write.ExpectedVersion)
        {
            return false;
        }

        database.Seats.Add(new Seat
        {
            Id = write.Seat.Id,
            SessionId = write.SessionId,
            Order = write.Seat.Order,
            Kind = write.Seat.Kind,
            DisplayName = write.Seat.DisplayName,
            Symbol = write.Seat.Symbol,
            UserId = write.Seat.UserId,
            AiDifficulty = write.Seat.AiDifficulty,
            AiStrategyVersion = write.Seat.AiStrategyVersion,
            ConnectionStatus = write.Seat.ConnectionStatus,
            DisconnectedAt = write.Seat.DisconnectedAt,
        });

        session.StateVersion = write.ExpectedVersion + 1;
        session.UpdatedAt = write.UpdatedAt;
        session.LastActivityAt = write.UpdatedAt;

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> RemoveSeatAsync(SeatRemoveWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        // Seats are moved up one by one in ascending order inside one transaction, so the unique index
        // (SessionId, Order) is never violated. The execution strategy retries the whole unit.
        var strategy = database.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async ct =>
            {
                database.ChangeTracker.Clear();
                await using var transaction = await database.Database.BeginTransactionAsync(ct).ConfigureAwait(false);

                var bumped = await database.GameSessions
                    .Where(s => s.Id == write.SessionId && s.StateVersion == write.ExpectedVersion && s.Status == SessionStatus.Lobby)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(s => s.StateVersion, write.ExpectedVersion + 1)
                            .SetProperty(s => s.UpdatedAt, write.UpdatedAt)
                            .SetProperty(s => s.LastActivityAt, write.UpdatedAt),
                        ct)
                    .ConfigureAwait(false);
                if (bumped == 0)
                {
                    return false;
                }

                var deleted = await database.Seats
                    .Where(seat => seat.Id == write.SeatId && seat.SessionId == write.SessionId)
                    .ExecuteDeleteAsync(ct)
                    .ConfigureAwait(false);
                if (deleted == 0)
                {
                    return false;
                }

                foreach (var reorder in write.Reorders.OrderBy(r => r.Order))
                {
                    await database.Seats
                        .Where(seat => seat.Id == reorder.SeatId && seat.SessionId == write.SessionId)
                        .ExecuteUpdateAsync(
                            setters => setters
                                .SetProperty(seat => seat.Order, reorder.Order)
                                .SetProperty(seat => seat.Symbol, reorder.Symbol),
                            ct)
                        .ConfigureAwait(false);
                }

                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return true;
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<OpenLobbySummary>> GetOpenLobbiesAsync(string? gameType, int limit, CancellationToken cancellationToken) =>
        await database.GameSessions.AsNoTracking()
            .Where(s => s.Mode == GameMode.Multiplayer && s.Status == SessionStatus.Lobby)
            .Where(s => gameType == null || s.GameType == gameType)
            .OrderByDescending(s => s.CreatedAt)
            .Take(limit)
            .Select(s => new OpenLobbySummary(
                s.Id,
                s.GameType,
                s.OwnerUserId,
                s.Seats.Where(seat => seat.UserId == s.OwnerUserId).Select(seat => seat.DisplayName).FirstOrDefault() ?? string.Empty,
                s.Seats.Count,
                s.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> StartSessionAsync(SessionStartWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        var session = await database.GameSessions
            .FirstOrDefaultAsync(s => s.Id == write.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.StateVersion != write.ExpectedVersion || session.Status != SessionStatus.Lobby)
        {
            return false;
        }

        session.Status = SessionStatus.Running;
        session.CurrentSeatId = write.CurrentSeatId;
        session.StateJson = write.StateJson;
        session.StateVersion = write.ExpectedVersion + 1;
        session.InviteCode = null;
        session.UpdatedAt = write.UpdatedAt;
        session.LastActivityAt = write.UpdatedAt;
        session.TurnStartedAt = write.UpdatedAt;

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            database.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<bool> SaveActionAsync(ActionWrite write, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(write);

        var session = await database.GameSessions
            .FirstOrDefaultAsync(s => s.Id == write.SessionId, cancellationToken)
            .ConfigureAwait(false);
        if (session is null || session.StateVersion != write.ExpectedVersion)
        {
            return false;
        }

        var action = write.Action;
        session.StateJson = write.NewStateJson;
        session.StateVersion = write.ExpectedVersion + 1;
        session.Status = write.NewStatus;
        session.CurrentSeatId = write.CurrentSeatId;
        session.UpdatedAt = action.CreatedAt;
        session.LastActivityAt = action.CreatedAt;
        if (write.NewStatus != SessionStatus.Running)
        {
            session.TurnStartedAt = null;
        }
        else if (write.TurnStartedAt is { } turnStartedAt)
        {
            session.TurnStartedAt = turnStartedAt;
        }

        database.GameActions.Add(new GameAction
        {
            SessionId = action.SessionId,
            SeatId = action.SeatId,
            Sequence = action.Sequence,
            ActionType = action.ActionType,
            PayloadJson = action.PayloadJson,
            RandomResultJson = action.RandomResultJson,
            AiStrategyVersion = action.AiStrategyVersion,
            CreatedAt = action.CreatedAt,
        });

        if (write.SeatChanges is { Count: > 0 } changes)
        {
            var seatIds = changes.Select(change => change.SeatId).ToList();
            var seats = await database.Seats
                .Where(seat => seat.SessionId == write.SessionId && seatIds.Contains(seat.Id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var change in changes)
            {
                var seat = seats.FirstOrDefault(candidate => candidate.Id == change.SeatId);
                if (seat is not null)
                {
                    seat.ConnectionStatus = change.Status;
                    seat.DisconnectedAt = change.DisconnectedAt;
                }
            }
        }

        foreach (var result in write.Results)
        {
            database.GameResults.Add(new GameResult
            {
                SessionId = write.SessionId,
                SeatId = result.SeatId,
                Outcome = result.Outcome,
                Placement = result.Placement,
                Score = result.Score,
            });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException)
        {
            // Concurrency token (StateVersion) or unique (SessionId, Sequence): another action was faster.
            database.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task<IReadOnlyList<GameActionRecord>> GetActionsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        await database.GameActions.AsNoTracking()
            .Where(a => a.SessionId == sessionId)
            .OrderBy(a => a.Sequence)
            .Select(a => new GameActionRecord(
                a.SessionId, a.SeatId, a.Sequence, a.ActionType, a.PayloadJson, a.RandomResultJson, a.AiStrategyVersion, a.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<Guid>> GetSessionIdsForUserAsync(
        Guid userId,
        IReadOnlyCollection<SessionStatus> statuses,
        CancellationToken cancellationToken) =>
        await database.Seats.AsNoTracking()
            .Where(seat => seat.UserId == userId && statuses.Contains(seat.Session!.Status))
            .Select(seat => seat.SessionId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PlayerSeatRef>> GetMultiplayerSeatsAsync(SeatConnectionStatus status, CancellationToken cancellationToken) =>
        await database.Seats.AsNoTracking()
            .Where(seat => seat.Kind == SeatKind.Human
                && seat.UserId != null
                && seat.ConnectionStatus == status
                && seat.Session!.Mode == GameMode.Multiplayer
                && seat.Session.Status == SessionStatus.Running)
            .Select(seat => new PlayerSeatRef(seat.SessionId, seat.Id, seat.UserId!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PlayerSeatRef>> GetExpiredDisconnectedSeatsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await database.Seats.AsNoTracking()
            .Where(seat => seat.Kind == SeatKind.Human
                && seat.UserId != null
                && seat.ConnectionStatus == SeatConnectionStatus.Disconnected
                && seat.DisconnectedAt != null
                && seat.DisconnectedAt <= cutoff
                && seat.Session!.Mode == GameMode.Multiplayer
                && seat.Session.Status == SessionStatus.Running)
            .Select(seat => new PlayerSeatRef(seat.SessionId, seat.Id, seat.UserId!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<PlayerSeatRef>> GetTurnTimeoutsAsync(DateTimeOffset cutoff, CancellationToken cancellationToken) =>
        await database.GameSessions.AsNoTracking()
            .Where(session => session.Mode == GameMode.Multiplayer
                && session.Status == SessionStatus.Running
                && session.TurnStartedAt != null
                && session.TurnStartedAt <= cutoff)
            .Join(
                database.Seats.AsNoTracking().Where(seat => seat.Kind == SeatKind.Human
                    && seat.UserId != null
                    && seat.ConnectionStatus == SeatConnectionStatus.Connected),
                session => session.CurrentSeatId,
                seat => (Guid?)seat.Id,
                (session, seat) => new PlayerSeatRef(session.Id, seat.Id, seat.UserId!.Value))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public async Task<bool> SetStatusAsync(
        Guid sessionId,
        int expectedVersion,
        SessionStatus status,
        DateTimeOffset at,
        CancellationToken cancellationToken) =>
        await database.GameSessions
            .Where(session => session.Id == sessionId && session.StateVersion == expectedVersion)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(session => session.Status, status)
                    .SetProperty(session => session.CurrentSeatId, (Guid?)null)
                    .SetProperty(session => session.TurnStartedAt, (DateTimeOffset?)null)
                    .SetProperty(session => session.StateVersion, expectedVersion + 1)
                    .SetProperty(session => session.UpdatedAt, at),
                cancellationToken)
            .ConfigureAwait(false) == 1;

    public async Task<IReadOnlyList<Guid>> GetRunningSessionsWithAiTurnAsync(CancellationToken cancellationToken) =>
        await database.GameSessions.AsNoTracking()
            .Where(session => session.Status == SessionStatus.Running)
            .Join(
                database.Seats.AsNoTracking().Where(seat => seat.Kind == SeatKind.Ai),
                session => session.CurrentSeatId,
                seat => seat.Id,
                (session, _) => session.Id)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private async Task<SessionSnapshot> ToSnapshotAsync(GameSession session, CancellationToken cancellationToken)
    {
        var lastSequence = await database.GameActions.AsNoTracking()
            .Where(a => a.SessionId == session.Id)
            .MaxAsync(a => (int?)a.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        return new SessionSnapshot(
            session.Id,
            session.GameType,
            session.Mode,
            session.Status,
            session.OwnerUserId,
            session.InviteCode,
            session.CurrentSeatId,
            session.StateJson,
            session.StateVersion,
            session.RngSeed,
            lastSequence,
            [.. session.Seats.OrderBy(s => s.Order).Select(s =>
                new SeatSnapshot(
                    s.Id,
                    s.Order,
                    s.Kind,
                    s.DisplayName,
                    s.Symbol,
                    s.UserId,
                    s.ConnectionStatus,
                    s.AiDifficulty,
                    s.AiStrategyVersion))])
        {
            TurnStartedAt = session.TurnStartedAt,
        };
    }
}
