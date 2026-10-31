using Logic.TicTacToe;

namespace Logic.Sessions;

/// <summary>Mark shown for a seat position: X and O in Tic-Tac-Toe, white and black in Mühle; Kniffel uses none.</summary>
public static class SeatSymbols
{
    public static string? For(string gameType, int order) => (gameType, order) switch
    {
        (TicTacToeModule.Type, 0) => "X",
        (TicTacToeModule.Type, 1) => "O",
        (global::Shared.Models.Games.GameTypes.Muehle, 0) => "Weiß",
        (global::Shared.Models.Games.GameTypes.Muehle, 1) => "Schwarz",
        _ => null,
    };
}
