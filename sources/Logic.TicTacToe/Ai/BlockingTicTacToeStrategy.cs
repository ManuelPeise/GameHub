using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

/// <summary>Medium: takes an own winning move, otherwise blocks the opponent's winning move, otherwise random.</summary>
public sealed class BlockingTicTacToeStrategy : IAiStrategy<TicTacToeView, PlaceMark>
{
    public string Difficulty => AiDifficulties.Medium;

    public string Version => "medium-1";

    public PlaceMark ChooseAction(TicTacToeView view, int seat, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(random);

        if (view.LegalCells.Count == 0)
        {
            throw new InvalidOperationException("No legal action available.");
        }

        var me = TicTacToeState.SymbolOf(seat);
        var candidates = TicTacToeBoard.WinningCells(view.Board, me);
        if (candidates.Count == 0)
        {
            candidates = TicTacToeBoard.WinningCells(view.Board, TicTacToeBoard.Opponent(me));
        }

        if (candidates.Count == 0)
        {
            candidates = [.. view.LegalCells];
        }

        return new PlaceMark(candidates[random.Next(candidates.Count)]);
    }
}
