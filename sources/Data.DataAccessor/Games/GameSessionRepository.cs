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
        if (session is null)
        {
            return null;
        }

        var lastSequence = await database.GameActions
            .Where(a => a.SessionId == sessionId)
            .MaxAsync(a => (int?)a.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        return new SessionSnapshot(
            session.Id,
            session.GameType,
            session.Mode,
            session.Status,
            session.CurrentSeatId,
            session.StateJson,
            session.StateVersion,
            session.RngSeed,
            lastSequence,
            [.. session.Seats.OrderBy(s => s.Order).Select(s =>
                new SeatSnapshot(s.Id, s.Order, s.Kind, s.UserId, s.ConnectionStatus, s.AiDifficulty, s.AiStrategyVersion))]);
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
}
