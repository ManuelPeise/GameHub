using Data.Database;
using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Models.Games;

namespace Data.DataAccessor.Statistics;

/// <summary>EF Core adapter for <see cref="IStatisticsStore"/>: results of human seats in finished sessions.</summary>
public sealed class StatisticsStore(GameDbContext database) : IStatisticsStore
{
    public async Task<IReadOnlyList<ResultRow>> GetResultsAsync(
        Guid? userId,
        GameMode? mode,
        string? gameType,
        DateTimeOffset? since,
        CancellationToken cancellationToken)
    {
        var query =
            from result in database.GameResults.AsNoTracking()
            join seat in database.Seats.AsNoTracking() on result.SeatId equals seat.Id
            join session in database.GameSessions.AsNoTracking() on result.SessionId equals session.Id
            join user in database.Users.AsNoTracking() on seat.UserId equals (Guid?)user.Id
            where session.Status == SessionStatus.Finished && seat.Kind == SeatKind.Human
            select new { result, session, user };

        if (userId is { } id)
        {
            query = query.Where(row => row.user.Id == id);
        }

        if (mode is { } gameMode)
        {
            query = query.Where(row => row.session.Mode == gameMode);
        }

        if (!string.IsNullOrEmpty(gameType))
        {
            query = query.Where(row => row.session.GameType == gameType);
        }

        if (since is { } from)
        {
            query = query.Where(row => row.session.UpdatedAt >= from);
        }

        return await query
            .Select(row => new ResultRow(
                row.session.Id,
                row.session.GameType,
                row.session.Mode,
                row.session.UpdatedAt,
                row.user.Id,
                row.user.DisplayName,
                row.user.Status == UserStatus.Deleted,
                row.result.Outcome,
                row.result.Score,
                database.Seats
                    .Where(other => other.SessionId == row.session.Id && other.Kind == SeatKind.Ai)
                    .Select(other => other.AiDifficulty)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
