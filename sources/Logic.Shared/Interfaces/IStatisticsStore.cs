using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Shared.Interfaces;

/// <summary>Port for reading the results of finished sessions (human seats only).</summary>
public interface IStatisticsStore
{
    /// <param name="userId">Only this user; null = all users.</param>
    /// <param name="mode">Only this mode; null = all modes.</param>
    /// <param name="gameType">Only this game; null = all games.</param>
    /// <param name="since">Only sessions that ended at or after this time; null = all.</param>
    Task<IReadOnlyList<ResultRow>> GetResultsAsync(
        Guid? userId,
        GameMode? mode,
        string? gameType,
        DateTimeOffset? since,
        CancellationToken cancellationToken);
}
