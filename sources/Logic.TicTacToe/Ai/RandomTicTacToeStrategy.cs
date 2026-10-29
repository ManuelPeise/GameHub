using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

/// <summary>Easy: a random legal move.</summary>
public sealed class RandomTicTacToeStrategy : IAiStrategy<TicTacToeView, PlaceMark>
{
    public string Difficulty => AiDifficulties.Easy;

    public string Version => "easy-1";

    public PlaceMark ChooseAction(TicTacToeView view, int seat, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(random);

        if (view.LegalCells.Count == 0)
        {
            throw new InvalidOperationException("No legal action available.");
        }

        var index = random.Next(view.LegalCells.Count);
        return new PlaceMark(view.LegalCells[index]);
    }
}
