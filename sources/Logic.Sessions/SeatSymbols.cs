namespace Logic.Sessions;

/// <summary>Mark shown for a seat position (Tic-Tac-Toe: X and O; other games show none).</summary>
public static class SeatSymbols
{
    public static string? For(int order) => order switch
    {
        0 => "X",
        1 => "O",
        _ => null,
    };
}
