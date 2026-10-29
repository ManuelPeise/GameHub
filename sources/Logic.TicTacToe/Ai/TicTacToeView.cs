using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

public sealed record TicTacToeView(
    string Board,
    int NextSeat,
    TicTacToeStatus Status,
    int? WinnerSeat,
    IReadOnlyList<int>? WinningLine,
    IReadOnlyList<int> LegalCells);
