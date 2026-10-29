using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

/// <summary>Board helpers shared by the AI strategies.</summary>
internal static class TicTacToeBoard
{
    public static char Opponent(char symbol) => symbol == 'X' ? 'O' : 'X';

    /// <summary>Empty cells that complete a line for the symbol, in ascending order.</summary>
    public static List<int> WinningCells(string board, char symbol) =>
    [
        .. Enumerable.Range(0, TicTacToeState.CellCount)
            .Where(cell => board[cell] == TicTacToeState.EmptyCell && Wins(Place(board, cell, symbol), symbol)),
    ];

    public static string Place(string board, int cell, char symbol)
    {
        var cells = board.ToCharArray();
        cells[cell] = symbol;
        return new string(cells);
    }

    public static bool Wins(string board, char symbol) =>
        TicTacToeRules.Lines.Any(line => line.All(index => board[index] == symbol));
}
