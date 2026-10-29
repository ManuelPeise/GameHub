using Logic.Core.Randomness;
using Logic.TicTacToe.Ai;
using Logic.TicTacToe.Domain;

namespace Tests.TicTacToe;

public sealed class RandomTicTacToeStrategyTests
{
    private readonly TicTacToeRules _rules = new();
    private readonly RandomTicTacToeStrategy _strategy = new();

    [Xunit.Fact]
    public void Strategy_OnlyReturnsLegalActions_ForManySeeds()
    {
        var state = _rules.CreateInitialState(2, new SeededRandom(1));
        var legal = _rules.LegalActions(state, seat: 0).Select(action => action.Cell).ToHashSet();
        var view = new TicTacToeView(state.Board, state.NextSeat, state.Status, state.WinnerSeat, state.WinningLine, [.. legal]);

        foreach (var seed in Enumerable.Range(1, 500))
        {
            var action = _strategy.ChooseAction(view, seat: 0, SeededRandom.ForAction(seed, 1));
            Xunit.Assert.Contains(action.Cell, legal);
        }
    }

    [Xunit.Fact]
    public void Strategy_ChoosesDeterministically_ForSameSeed()
    {
        var state = _rules.CreateInitialState(2, new SeededRandom(1));
        var legalCells = _rules.LegalActions(state, seat: 0).Select(action => action.Cell).ToArray();
        var view = new TicTacToeView(state.Board, state.NextSeat, state.Status, state.WinnerSeat, state.WinningLine, legalCells);

        var first = _strategy.ChooseAction(view, seat: 0, SeededRandom.ForAction(42, 7));
        var second = _strategy.ChooseAction(view, seat: 0, SeededRandom.ForAction(42, 7));

        Xunit.Assert.Equal(first, second);
    }
}
