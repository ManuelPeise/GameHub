using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

/// <summary>
/// Hard: minimax (negamax) with alpha-beta pruning. Faster wins and later losses score better; among
/// equally good moves one is chosen at random via the session seed. Never loses.
/// </summary>
public sealed class MinimaxTicTacToeStrategy : IAiStrategy<TicTacToeView, PlaceMark>
{
    private const int WinScore = 10;

    public string Difficulty => AiDifficulties.Hard;

    public string Version => "hard-1";

    public PlaceMark ChooseAction(TicTacToeView view, int seat, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(random);

        if (view.LegalCells.Count == 0)
        {
            throw new InvalidOperationException("No legal action available.");
        }

        var me = TicTacToeState.SymbolOf(seat);
        var best = new List<int>();
        var bestScore = int.MinValue;

        // Every root move is evaluated with a full window so that equal scores are exact (fair tie-break).
        foreach (var cell in view.LegalCells.Order())
        {
            var score = ScoreMove(view.Board, cell, me, depth: 1, int.MinValue + 1, int.MaxValue);
            if (score > bestScore)
            {
                bestScore = score;
                best.Clear();
            }

            if (score == bestScore)
            {
                best.Add(cell);
            }
        }

        return new PlaceMark(best[random.Next(best.Count)]);
    }

    /// <summary>Value of placing <paramref name="symbol"/> on <paramref name="cell"/>, seen from that player.</summary>
    private static int ScoreMove(string board, int cell, char symbol, int depth, int alpha, int beta)
    {
        var next = TicTacToeBoard.Place(board, cell, symbol);
        if (TicTacToeBoard.Wins(next, symbol))
        {
            return WinScore - depth;
        }

        if (!next.Contains(TicTacToeState.EmptyCell, StringComparison.Ordinal))
        {
            return 0;
        }

        return -BestReply(next, TicTacToeBoard.Opponent(symbol), depth + 1, -beta, -alpha);
    }

    /// <summary>Best achievable value for the player to move (negamax with alpha-beta).</summary>
    private static int BestReply(string board, char symbol, int depth, int alpha, int beta)
    {
        var best = int.MinValue + 1;
        for (var cell = 0; cell < TicTacToeState.CellCount; cell++)
        {
            if (board[cell] != TicTacToeState.EmptyCell)
            {
                continue;
            }

            var score = ScoreMove(board, cell, symbol, depth, alpha, beta);
            best = Math.Max(best, score);
            alpha = Math.Max(alpha, score);
            if (alpha >= beta)
            {
                break;
            }
        }

        return best;
    }
}
