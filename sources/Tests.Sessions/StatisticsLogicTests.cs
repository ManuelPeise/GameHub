using Logic.Sessions.Statistics;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;
using Shared.Models.Statistics;

namespace Tests.Sessions;

public sealed class StatisticsLogicTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _bob = Guid.NewGuid();
    private readonly Guid _carla = Guid.NewGuid();
    private readonly Guid _deleted = Guid.NewGuid();
    private readonly List<ResultRow> _rows = [];

    private StatisticsLogic Logic() => new(new FakeStore(_rows), new FixedTime(Now));

    private void Add(Guid user, string name, string game, GameMode mode, GameOutcome outcome, int? score = null,
        string? difficulty = null, int daysAgo = 1, bool deleted = false) =>
        _rows.Add(new ResultRow(Guid.NewGuid(), game, mode, Now.AddDays(-daysAgo), user, name, deleted, outcome, score, difficulty));

    [Xunit.Fact]
    public async Task Personal_CountsPerGameAndMode_SplitsAiByDifficulty_AndKniffelScores()
    {
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Singleplayer, GameOutcome.Win, difficulty: "Easy");
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Singleplayer, GameOutcome.Loss, difficulty: "Hard");
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Singleplayer, GameOutcome.Draw, difficulty: "Hard");
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Removed);
        Add(_me, "Ich", GameTypes.Kniffel, GameMode.Multiplayer, GameOutcome.Win, 250);
        Add(_me, "Ich", GameTypes.Kniffel, GameMode.Multiplayer, GameOutcome.Loss, 180);

        var stats = await Logic().GetPersonalAsync(_me, CancellationToken.None);

        var hard = stats.Rows.Single(row => row.GameType == GameTypes.TicTacToe && row.AiDifficulty == "Hard");
        Xunit.Assert.Equal((2, 0, 1, 1), (hard.Games, hard.Wins, hard.Losses, hard.Draws));
        Xunit.Assert.Single(stats.Rows, row => row.GameType == GameTypes.TicTacToe && row.AiDifficulty == "Easy");

        var multiplayer = stats.Rows.Single(row => row.GameType == GameTypes.TicTacToe && row.Mode == GameMode.Multiplayer);
        Xunit.Assert.Equal(1, multiplayer.Losses); // removed counts as loss
        Xunit.Assert.Null(multiplayer.AiDifficulty);

        var kniffel = stats.Rows.Single(row => row.GameType == GameTypes.Kniffel);
        Xunit.Assert.Equal(215.0, kniffel.AverageScore);
        Xunit.Assert.Equal(250, kniffel.BestScore);
    }

    [Xunit.Fact]
    public async Task Leaderboard_SortsByWinsThenRate_AndSharesRanks()
    {
        // me: 2 wins of 2; bob: 2 wins of 4; carla: 2 wins of 2 (same as me)
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);
        Add(_me, "Ich", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);
        Add(_bob, "Bob", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);
        Add(_bob, "Bob", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);
        Add(_bob, "Bob", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Loss);
        Add(_bob, "Bob", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Loss);
        Add(_carla, "Carla", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);
        Add(_carla, "Carla", GameTypes.TicTacToe, GameMode.Multiplayer, GameOutcome.Win);

        var board = await Logic().GetLeaderboardAsync(_me, GameTypes.TicTacToe, StatisticsPeriods.All, CancellationToken.None);

        Xunit.Assert.Equal(new[] { 1, 1, 3 }, board.Entries.Select(entry => entry.Rank).ToArray());
        Xunit.Assert.Equal("Bob", board.Entries[2].DisplayName);
        Xunit.Assert.Equal(0.5, board.Entries[2].WinRate);
        Xunit.Assert.Single(board.Entries, entry => entry.IsMe);
    }

    [Xunit.Fact]
    public async Task Leaderboard_Last30Days_IgnoresOlderGames_AndDeletedPlayersAreAnonymous()
    {
        Add(_bob, "Bob", GameTypes.Kniffel, GameMode.Multiplayer, GameOutcome.Win, 300, daysAgo: 40);
        Add(_deleted, "Max Mustermann", GameTypes.Kniffel, GameMode.Multiplayer, GameOutcome.Win, 280, daysAgo: 2, deleted: true);

        var recent = await Logic().GetLeaderboardAsync(_me, GameTypes.Kniffel, StatisticsPeriods.Last30Days, CancellationToken.None);
        var all = await Logic().GetLeaderboardAsync(_me, GameTypes.Kniffel, "unknown", CancellationToken.None);

        var entry = Xunit.Assert.Single(recent.Entries);
        Xunit.Assert.Equal("Gelöschter Spieler", entry.DisplayName);
        Xunit.Assert.Equal(280, Xunit.Assert.Single(recent.HighScores).Score);
        Xunit.Assert.Equal(StatisticsPeriods.All, all.Period);
        Xunit.Assert.Equal(new[] { 300, 280 }, all.HighScores.Select(score => score.Score).ToArray());
    }

    /// <summary>Applies the same filters as the database query.</summary>
    private sealed class FakeStore(List<ResultRow> rows) : IStatisticsStore
    {
        public Task<IReadOnlyList<ResultRow>> GetResultsAsync(
            Guid? userId, GameMode? mode, string? gameType, DateTimeOffset? since, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ResultRow>>(
            [
                .. rows.Where(row => (userId is null || row.UserId == userId)
                    && (mode is null || row.Mode == mode)
                    && (gameType is null || row.GameType == gameType)
                    && (since is null || row.EndedAt >= since)),
            ]);
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
