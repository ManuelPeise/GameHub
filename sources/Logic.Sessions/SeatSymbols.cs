using Logic.TicTacToe;

namespace Logic.Sessions;

/// <summary>Mark shown for a seat position: X and O in Tic-Tac-Toe; Kniffel uses none.</summary>
public static class SeatSymbols
{
    public static string? For(string gameType, int order) => gameType == TicTacToeModule.Type
        ? order switch
        {
            0 => "X",
            1 => "O",
            _ => null,
        }
        : null;
}
