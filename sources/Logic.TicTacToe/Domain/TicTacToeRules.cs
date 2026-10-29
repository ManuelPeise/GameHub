using Logic.Core.Abstractions;
using Logic.Core.Model;

namespace Logic.TicTacToe.Domain;

/// <summary>Rules of Tic-Tac-Toe on a 3×3 board for two seats.</summary>
public sealed class TicTacToeRules : IGameRules<TicTacToeState, PlaceMark>
{
    /// <summary>All 8 winning lines: 3 rows, 3 columns, 2 diagonals.</summary>
    public static IReadOnlyList<int[]> Lines { get; } =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6],
    ];

    public int MinSeats => 2;

    public int MaxSeats => 2;

    public TicTacToeState CreateInitialState(int seatCount, IRandom random)
    {
        if (seatCount != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "Tic-Tac-Toe needs exactly two seats.");
        }

        return TicTacToeState.Initial;
    }

    public ValidationResult Validate(TicTacToeState state, int seat, PlaceMark action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        if (state.Status != TicTacToeStatus.Running)
        {
            return ValidationResult.Invalid("Die Partie ist beendet.");
        }

        if (seat != state.NextSeat)
        {
            return ValidationResult.Invalid("Du bist nicht am Zug.");
        }

        if (action.Cell is < 0 or >= TicTacToeState.CellCount)
        {
            return ValidationResult.Invalid("Dieses Feld gibt es nicht.");
        }

        if (state.Board[action.Cell] != TicTacToeState.EmptyCell)
        {
            return ValidationResult.Invalid("Das Feld ist bereits belegt.");
        }

        return ValidationResult.Valid;
    }

    public TicTacToeState Apply(TicTacToeState state, int seat, PlaceMark action, IRandom random)
    {
        var validation = Validate(state, seat, action);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var cells = state.Board.ToCharArray();
        cells[action.Cell] = TicTacToeState.SymbolOf(seat);
        var board = new string(cells);

        var winningLine = FindWinningLine(board);
        if (winningLine is not null)
        {
            return state with { Board = board, Status = TicTacToeStatus.Won, WinnerSeat = seat, WinningLine = winningLine };
        }

        if (!board.Contains(TicTacToeState.EmptyCell, StringComparison.Ordinal))
        {
            return state with { Board = board, Status = TicTacToeStatus.Draw };
        }

        return state with { Board = board, NextSeat = 1 - seat };
    }

    public IReadOnlyList<PlaceMark> LegalActions(TicTacToeState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != TicTacToeStatus.Running || seat != state.NextSeat)
        {
            return [];
        }

        return [.. Enumerable.Range(0, TicTacToeState.CellCount)
            .Where(cell => state.Board[cell] == TicTacToeState.EmptyCell)
            .Select(cell => new PlaceMark(cell))];
    }

    public GameProgress GetProgress(TicTacToeState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Status switch
        {
            TicTacToeStatus.Won when state.WinnerSeat is { } winner => GameProgress.Finished(
            [
                new SeatResult(winner, SeatOutcome.Win, 1, null),
                new SeatResult(1 - winner, SeatOutcome.Loss, 2, null),
            ]),
            TicTacToeStatus.Draw => GameProgress.Finished(
            [
                new SeatResult(0, SeatOutcome.Draw, 1, null),
                new SeatResult(1, SeatOutcome.Draw, 1, null),
            ]),
            _ => GameProgress.Running(state.NextSeat),
        };
    }

    private static int[]? FindWinningLine(string board) =>
        Lines.FirstOrDefault(line =>
            board[line[0]] != TicTacToeState.EmptyCell
            && board[line[0]] == board[line[1]]
            && board[line[1]] == board[line[2]]);
}
