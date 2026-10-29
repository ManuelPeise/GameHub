using Logic.Core.Ai;
using Logic.Core.Randomness;
using Logic.TicTacToe.Ai;
using Logic.TicTacToe.Domain;

namespace Tests.TicTacToe;

public sealed class TicTacToeStrategyTests
{
    private static readonly TicTacToeRules Rules = new();

    public static Xunit.TheoryData<string> Difficulties() => ["Easy", "Medium", "Hard"];

    private static IAiStrategy<TicTacToeView, PlaceMark> Create(string difficulty) => difficulty switch
    {
        "Easy" => new RandomTicTacToeStrategy(),
        "Medium" => new BlockingTicTacToeStrategy(),
        _ => new MinimaxTicTacToeStrategy(),
    };

    private static TicTacToeView View(TicTacToeState state, int seat) =>
        new(state.Board, state.NextSeat, state.Status, state.WinnerSeat, state.WinningLine,
            [.. Rules.LegalActions(state, seat).Select(action => action.Cell)]);

    private static TicTacToeState FromBoard(string board, int nextSeat) =>
        new(board, nextSeat, TicTacToeStatus.Running, null, null);

    [Xunit.Theory]
    [Xunit.MemberData(nameof(Difficulties))]
    public void AllStrategies_OnlyPlayLegalMoves(string difficulty)
    {
        // Property test: many self-play games with different seeds, every move must be valid.
        var strategy = Create(difficulty);
        for (var seed = 0; seed < 200; seed++)
        {
            var random = new SeededRandom(seed);
            var state = Rules.CreateInitialState(2, random);
            while (state.Status == TicTacToeStatus.Running)
            {
                var move = strategy.ChooseAction(View(state, state.NextSeat), state.NextSeat, random);
                Xunit.Assert.True(Rules.Validate(state, state.NextSeat, move).IsValid, $"{difficulty}, seed {seed}: {move}");
                state = Rules.Apply(state, state.NextSeat, move, random);
            }
        }
    }

    [Xunit.Theory]
    [Xunit.MemberData(nameof(Difficulties))]
    public void SameSeed_GivesSameDecisions(string difficulty)
    {
        var strategy = Create(difficulty);
        var state = FromBoard("X---O----", 0);

        for (var seed = 0; seed < 20; seed++)
        {
            var first = strategy.ChooseAction(View(state, 0), 0, new SeededRandom(seed));
            var second = strategy.ChooseAction(View(state, 0), 0, new SeededRandom(seed));
            Xunit.Assert.Equal(first, second);
        }
    }

    [Xunit.Fact]
    public void Medium_TakesTheWinningMove()
    {
        // X X - / O O - / - - -  → X wins with cell 2 (instead of blocking 5)
        var move = new BlockingTicTacToeStrategy().ChooseAction(View(FromBoard("XX-OO----", 0), 0), 0, new SeededRandom(1));

        Xunit.Assert.Equal(2, move.Cell);
    }

    [Xunit.Fact]
    public void Medium_BlocksTheOpponent()
    {
        // X X - / O - - / - - O  → O must block cell 2
        var move = new BlockingTicTacToeStrategy().ChooseAction(View(FromBoard("XX-O----O", 1), 1), 1, new SeededRandom(1));

        Xunit.Assert.Equal(2, move.Cell);
    }

    [Xunit.Fact]
    public void Hard_PrefersTheFasterWin()
    {
        // X X - / O O - / - - -  with X to move: cell 2 wins at once.
        var move = new MinimaxTicTacToeStrategy().ChooseAction(View(FromBoard("XX-OO----", 0), 0), 0, new SeededRandom(1));

        Xunit.Assert.Equal(2, move.Cell);
    }

    [Xunit.Theory]
    [Xunit.InlineData(0)]
    [Xunit.InlineData(1)]
    public void Hard_NeverLoses_AgainstEveryPossibleSequence(int aiSeat)
    {
        // Exhaustive: the opponent tries every legal move at every point; the AI answers with its strategy.
        // Several seeds, because ties between equally good moves are broken at random.
        var strategy = new MinimaxTicTacToeStrategy();
        for (var seed = 0; seed < 5; seed++)
        {
            var games = 0;
            Explore(Rules.CreateInitialState(2, new SeededRandom(seed)), strategy, aiSeat, seed, ref games);
            Xunit.Assert.True(games > 0);
        }
    }

    private static void Explore(TicTacToeState state, MinimaxTicTacToeStrategy strategy, int aiSeat, int seed, ref int games)
    {
        if (state.Status != TicTacToeStatus.Running)
        {
            Xunit.Assert.False(
                state.Status == TicTacToeStatus.Won && state.WinnerSeat != aiSeat,
                $"Hard lost as seat {aiSeat} (seed {seed}): {state.Board}");
            games++;
            return;
        }

        var random = new SeededRandom(seed);
        if (state.NextSeat == aiSeat)
        {
            var move = strategy.ChooseAction(View(state, aiSeat), aiSeat, random);
            Explore(Rules.Apply(state, aiSeat, move, random), strategy, aiSeat, seed, ref games);
            return;
        }

        foreach (var move in Rules.LegalActions(state, state.NextSeat))
        {
            Explore(Rules.Apply(state, state.NextSeat, move, random), strategy, aiSeat, seed, ref games);
        }
    }
}
