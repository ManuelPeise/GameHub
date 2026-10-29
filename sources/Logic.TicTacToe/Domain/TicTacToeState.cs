namespace Logic.TicTacToe.Domain;

public enum TicTacToeStatus
{
    Running,
    Won,
    Draw,
}

/// <summary>
/// State of a Tic-Tac-Toe game. The board has 9 cells (row by row, 0 = top left), each '-', 'X' or 'O'.
/// Seat 0 plays X and starts, seat 1 plays O.
/// </summary>
public sealed record TicTacToeState(
    string Board,
    int NextSeat,
    TicTacToeStatus Status,
    int? WinnerSeat,
    IReadOnlyList<int>? WinningLine)
{
    public const char EmptyCell = '-';
    public const int CellCount = 9;

    public static TicTacToeState Initial { get; } =
        new(new string(EmptyCell, CellCount), 0, TicTacToeStatus.Running, null, null);

    public static char SymbolOf(int seat) => seat == 0 ? 'X' : 'O';
}

/// <summary>Action: put the own mark into an empty cell (0 to 8).</summary>
public sealed record PlaceMark(int Cell)
{
    public const string ActionType = "PlaceMark";
}
