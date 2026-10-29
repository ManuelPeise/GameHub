using Shared.Models.Games;

namespace Shared.Models.Statistics;

/// <summary>Leaderboard periods.</summary>
public static class StatisticsPeriods
{
    public const string All = "all";
    public const string Last30Days = "30d";
}

/// <summary>
/// One line of the personal statistics: a game in one mode; singleplayer lines are split by AI difficulty.
/// Removed (left the game, timed out) counts as a loss.
/// </summary>
public sealed record StatisticsRowDto(
    string GameType,
    GameMode Mode,
    string? AiDifficulty,
    int Games,
    int Wins,
    int Losses,
    int Draws,
    double? AverageScore,
    int? BestScore);

public sealed record PlayerStatisticsDto(IReadOnlyList<StatisticsRowDto> Rows);

/// <summary>Entry of the leaderboard (multiplayer only): most wins, then the higher win rate.</summary>
public sealed record LeaderboardEntryDto(int Rank, string DisplayName, bool IsMe, int Games, int Wins, double WinRate);

/// <summary>Best single result in Kniffel (multiplayer only).</summary>
public sealed record HighScoreDto(int Rank, string DisplayName, bool IsMe, int Score, DateTimeOffset AchievedAt);

public sealed record LeaderboardDto(
    string GameType,
    string Period,
    IReadOnlyList<LeaderboardEntryDto> Entries,
    IReadOnlyList<HighScoreDto> HighScores);
