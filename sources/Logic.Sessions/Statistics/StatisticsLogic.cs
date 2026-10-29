using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Account;
using Shared.Models.Games;
using Shared.Models.Statistics;

namespace Logic.Sessions.Statistics;

/// <summary>Personal statistics and the multiplayer leaderboard, computed from the stored results.</summary>
public sealed class StatisticsLogic(IStatisticsStore store, TimeProvider timeProvider)
{
    public const int LeaderboardSize = 20;

    public async Task<PlayerStatisticsDto> GetPersonalAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await store.GetResultsAsync(userId, mode: null, gameType: null, since: null, cancellationToken).ConfigureAwait(false);

        return new PlayerStatisticsDto(
        [
            .. rows
                .GroupBy(row => (row.GameType, row.Mode, Difficulty: row.Mode == GameMode.Singleplayer ? row.AiDifficulty : null))
                .Select(group =>
                {
                    var scores = group.Where(row => row.Score is not null).Select(row => row.Score!.Value).ToList();
                    return new StatisticsRowDto(
                        group.Key.GameType,
                        group.Key.Mode,
                        group.Key.Difficulty,
                        group.Count(),
                        group.Count(row => row.Outcome == GameOutcome.Win),
                        group.Count(row => row.Outcome is GameOutcome.Loss or GameOutcome.Removed),
                        group.Count(row => row.Outcome == GameOutcome.Draw),
                        scores.Count > 0 ? Math.Round(scores.Average(), 1) : null,
                        scores.Count > 0 ? scores.Max() : null);
                })
                .OrderBy(row => row.GameType, StringComparer.Ordinal)
                .ThenBy(row => row.Mode)
                .ThenBy(row => DifficultyOrder(row.AiDifficulty)),
        ]);
    }

    /// <summary>Multiplayer only: most wins, then higher win rate; Kniffel also the best single results.</summary>
    public async Task<LeaderboardDto> GetLeaderboardAsync(Guid userId, string gameType, string? period, CancellationToken cancellationToken)
    {
        var normalizedPeriod = period == StatisticsPeriods.Last30Days ? StatisticsPeriods.Last30Days : StatisticsPeriods.All;
        DateTimeOffset? since = normalizedPeriod == StatisticsPeriods.Last30Days ? timeProvider.GetUtcNow().AddDays(-30) : null;
        var rows = await store.GetResultsAsync(null, GameMode.Multiplayer, gameType, since, cancellationToken).ConfigureAwait(false);

        var players = rows
            .GroupBy(row => row.UserId)
            .Select(group =>
            {
                var games = group.Count();
                var wins = group.Count(row => row.Outcome == GameOutcome.Win);
                return (UserId: group.Key, Name: NameOf(group.First()), Games: games, Wins: wins, Rate: games == 0 ? 0 : (double)wins / games);
            })
            .Where(player => player.Wins > 0)
            .OrderByDescending(player => player.Wins)
            .ThenByDescending(player => player.Rate)
            .ThenBy(player => player.Name, StringComparer.CurrentCulture)
            .Take(LeaderboardSize)
            .ToList();

        var entries = players
            .Select(player => new LeaderboardEntryDto(
                1 + players.Count(other => other.Wins > player.Wins || (other.Wins == player.Wins && other.Rate > player.Rate)),
                player.Name,
                player.UserId == userId,
                player.Games,
                player.Wins,
                Math.Round(player.Rate, 3)))
            .ToList();

        var highScores = rows
            .Where(row => row.Score is not null)
            .OrderByDescending(row => row.Score)
            .ThenBy(row => row.EndedAt)
            .Take(LeaderboardSize)
            .ToList();

        return new LeaderboardDto(
            gameType,
            normalizedPeriod,
            entries,
            [
                .. highScores.Select(row => new HighScoreDto(
                    1 + highScores.Count(other => other.Score > row.Score),
                    NameOf(row),
                    row.UserId == userId,
                    row.Score!.Value,
                    row.EndedAt)),
            ]);
    }

    private static string NameOf(ResultRow row) => row.IsDeleted ? AccountRules.DeletedUserDisplayName : row.DisplayName;

    private static int DifficultyOrder(string? difficulty) => difficulty switch
    {
        "Easy" => 0,
        "Medium" => 1,
        "Hard" => 2,
        null => -1,
        _ => 3,
    };
}
