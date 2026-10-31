namespace Logic.Muehle.Domain;

public enum MuehleStatus
{
    Running,
    Won,
    Draw,
}

/// <summary>Why a game ended.</summary>
public enum MuehleEndReason
{
    /// <summary>The loser has fewer than three stones.</summary>
    TooFewStones,

    /// <summary>The loser cannot move.</summary>
    Blocked,

    /// <summary>The same position occurred for the third time.</summary>
    Repetition,

    /// <summary>50 moves per player without a capture.</summary>
    NoCapture,
}

/// <summary>Phase of one player.</summary>
public enum MuehlePhase
{
    Placing,
    Moving,
    Flying,
}

/// <summary>
/// State of a Mühle game. The board has 24 points (see <see cref="MuehleBoard"/>), each '-', 'W' or 'B'.
/// Seat 0 plays white and starts, seat 1 plays black.
/// </summary>
/// <param name="InHand">Stones each seat still has to place.</param>
/// <param name="PliesWithoutCapture">Moves of both players without a capture since both finished placing.</param>
/// <param name="Positions">Position keys since the last capture or placement (for the repetition rule).</param>
public sealed record MuehleState(
    string Board,
    int NextSeat,
    IReadOnlyList<int> InHand,
    MuehleStatus Status,
    int? WinnerSeat,
    MuehleEndReason? EndReason,
    int PliesWithoutCapture,
    IReadOnlyList<string> Positions,
    MuehleMove? LastMove)
{
    public const char EmptyPoint = '-';
    public const int StonesPerPlayer = 9;

    /// <summary>A draw after 50 moves per player without a capture.</summary>
    public const int NoCapturePlyLimit = 100;

    public static MuehleState Initial { get; } = new(
        new string(EmptyPoint, MuehleBoard.PointCount), 0, [StonesPerPlayer, StonesPerPlayer], MuehleStatus.Running, null, null, 0, [], null);

    public static char ColorOf(int seat) => seat == 0 ? 'W' : 'B';

    public int StonesOnBoard(int seat) => Board.Count(point => point == ColorOf(seat));

    public MuehlePhase PhaseOf(int seat) =>
        InHand[seat] > 0 ? MuehlePhase.Placing : StonesOnBoard(seat) == 3 ? MuehlePhase.Flying : MuehlePhase.Moving;

    /// <summary>Key of a position for the repetition rule: board and the seat to move.</summary>
    public static string PositionKey(string board, int nextSeat) => $"{board}{nextSeat}";
}

/// <summary>
/// Action: a complete turn. Without <see cref="From"/> a stone is placed on <see cref="To"/>, otherwise the stone
/// moves from <see cref="From"/> to <see cref="To"/>. <see cref="Remove"/> is the opponent stone taken when the
/// turn closes a mill.
/// </summary>
public sealed record MuehleMove(int? From, int To, int? Remove)
{
    public const string ActionType = "MuehleMove";
}
