namespace Logic.Core.Model;

/// <summary>Result of checking an action against the rules.</summary>
public sealed record ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Valid { get; } = new(true, null);

    /// <param name="error">Reason in the language of the UI (German).</param>
    public static ValidationResult Invalid(string error) => new(false, error);
}

/// <summary>Outcome of one seat at the end of a game.</summary>
public enum SeatOutcome
{
    Win,
    Loss,
    Draw,
}

/// <summary>Result of one seat (seat = position in the turn order, starting at 0).</summary>
public sealed record SeatResult(int Seat, SeatOutcome Outcome, int? Placement, int? Score);

/// <summary>State of a game seen from outside: finished, whose turn, results.</summary>
public sealed record GameProgress(bool IsFinished, int? CurrentSeat, IReadOnlyList<SeatResult> Results)
{
    public static GameProgress Running(int currentSeat) => new(false, currentSeat, []);

    public static GameProgress Finished(IReadOnlyList<SeatResult> results) => new(true, null, results);
}

/// <summary>Result of applying an action through <see cref="Abstractions.IGameModule"/>.</summary>
public abstract record ActionResult
{
    private ActionResult()
    {
    }

    public sealed record Applied(string StateJson, GameProgress Progress) : ActionResult;

    public sealed record Rejected(string Error) : ActionResult;
}

/// <summary>
/// Action types of the move log that are not game moves (connection and session events).
/// They do not change the game state and are skipped by the replay.
/// </summary>
public static class SystemActionTypes
{
    public const string Disconnected = "Disconnected";
    public const string Reconnected = "Reconnected";
    public const string PlayerRemoved = "PlayerRemoved";
    public const string TurnTimeout = "TurnTimeout";
    public const string Paused = "Paused";
    public const string Resumed = "Resumed";

    public static IReadOnlySet<string> All { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        Disconnected, Reconnected, PlayerRemoved, TurnTimeout, Paused, Resumed,
    };
}
