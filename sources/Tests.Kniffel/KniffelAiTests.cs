using System.Diagnostics;
using Logic.Core.Ai;
using Logic.Core.Randomness;
using Logic.Kniffel.Ai;
using Logic.Kniffel.Domain;

namespace Tests.Kniffel;

public sealed class KniffelAiTests
{
    private static readonly KniffelRules Rules = new();

    public static Xunit.TheoryData<string> Difficulties() => [AiDifficulties.Easy, AiDifficulties.Medium, AiDifficulties.Hard];

    private static KniffelAiStrategy Strategy(string difficulty) => difficulty switch
    {
        AiDifficulties.Easy => KniffelAiStrategy.Easy,
        AiDifficulties.Medium => KniffelAiStrategy.Medium,
        _ => KniffelAiStrategy.Hard,
    };

    /// <summary>Plays a two-seat game where both seats use the strategy; returns the total of seat 0.</summary>
    private static int PlayGame(KniffelAiStrategy strategy, int seed)
    {
        var random = new SeededRandom(seed);
        var state = Rules.CreateInitialState(2, random);
        while (state.Status == KniffelStatus.Running)
        {
            var seat = state.CurrentSeat;
            var action = strategy.ChooseAction(KniffelAiPlayer.ViewOf(state, seat), seat, random);
            var validation = Rules.Validate(state, seat, action);
            Xunit.Assert.True(validation.IsValid, $"{strategy.Difficulty}, seed {seed}: {action} – {validation.Error}");
            state = Rules.Apply(state, seat, action, random);
        }

        return KniffelScoring.Total(state.Sheets[0]);
    }

    [Xunit.Theory]
    [Xunit.MemberData(nameof(Difficulties))]
    public void AllLevels_OnlyPlayLegalActions(string difficulty)
    {
        for (var seed = 0; seed < 50; seed++)
        {
            PlayGame(Strategy(difficulty), seed);
        }
    }

    [Xunit.Theory]
    [Xunit.MemberData(nameof(Difficulties))]
    public void SameSeed_GivesTheSameGame(string difficulty)
    {
        Xunit.Assert.Equal(PlayGame(Strategy(difficulty), 7), PlayGame(Strategy(difficulty), 7));
    }

    [Xunit.Fact]
    public void ThousandGames_HardBeatsMedium_BeatsEasy_OnAverage()
    {
        const int games = 1000;
        var easy = Enumerable.Range(0, games).Average(seed => PlayGame(KniffelAiStrategy.Easy, seed));
        var medium = Enumerable.Range(0, games).Average(seed => PlayGame(KniffelAiStrategy.Medium, seed));
        var hard = Enumerable.Range(0, games).Average(seed => PlayGame(KniffelAiStrategy.Hard, seed));

        Xunit.Assert.True(hard > medium, $"Hard {hard:F1} should beat Medium {medium:F1}");
        Xunit.Assert.True(medium > easy, $"Medium {medium:F1} should beat Easy {easy:F1}");
    }

    [Xunit.Fact]
    public void Hard_DecidesInUnder50Milliseconds()
    {
        var state = Rules.Apply(Rules.CreateInitialState(2, new SeededRandom(3)), 0, new Roll(0), new SeededRandom(3));
        var view = KniffelAiPlayer.ViewOf(state, 0);
        KniffelAiStrategy.Hard.ChooseAction(view, 0, new SeededRandom(1)); // warm-up (static tables)

        var watch = Stopwatch.StartNew();
        for (var run = 0; run < 20; run++)
        {
            KniffelAiStrategy.Hard.ChooseAction(view, 0, new SeededRandom(run));
        }

        watch.Stop();
        Xunit.Assert.True(watch.Elapsed.TotalMilliseconds / 20 < 50, $"{watch.Elapsed.TotalMilliseconds / 20:F1} ms per decision");
    }

    [Xunit.Fact]
    public void Hard_KeepsAKniffel_AndScoresIt()
    {
        var state = Rules.CreateInitialState(2, new SeededRandom(1)) with { Dice = [6, 6, 6, 6, 6], RollsUsed = 1 };

        var action = KniffelAiStrategy.Hard.ChooseAction(KniffelAiPlayer.ViewOf(state, 0), 0, new SeededRandom(1));

        Xunit.Assert.Equal(new Score(KniffelCategory.Kniffel), action);
    }

    [Xunit.Fact]
    public void Ai_RespectsTheJokerRule()
    {
        var scores = KniffelSheet.Empty.Scores.ToArray();
        scores[(int)KniffelCategory.Kniffel] = 50;
        var sheet = new KniffelSheet(scores, 0);
        var state = Rules.CreateInitialState(2, new SeededRandom(1)) with
        {
            Sheets = [sheet, KniffelSheet.Empty],
            Dice = [3, 3, 3, 3, 3],
            RollsUsed = 3,
        };

        foreach (var difficulty in new[] { AiDifficulties.Easy, AiDifficulties.Medium, AiDifficulties.Hard })
        {
            var action = Strategy(difficulty).ChooseAction(KniffelAiPlayer.ViewOf(state, 0), 0, new SeededRandom(1));
            Xunit.Assert.Equal(new Score(KniffelCategory.Threes), action);
        }
    }
}
