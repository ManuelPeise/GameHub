using Logic.Core.Model;

namespace Logic.Sessions;

/// <summary>
/// An action of a seat (human or AI). <see cref="ExpectedVersion"/> is the state version the client saw;
/// null skips the check (e.g. for the AI runner, which reads the current version itself).
/// </summary>
public sealed record SubmitActionCommand(
    Guid SessionId,
    Guid SeatId,
    int? ExpectedVersion,
    string ActionType,
    string? PayloadJson,
    string? AiStrategyVersion = null);

public enum SubmitRejection
{
    NotFound,
    NotRunning,
    NotYourTurn,
    InvalidAction,

    /// <summary>Another action was stored first; the client has to reload the state.</summary>
    Conflict,
}

public abstract record SubmitActionResult
{
    private SubmitActionResult()
    {
    }

    public sealed record Applied(int StateVersion, int Sequence, string StateJson, GameProgress Progress) : SubmitActionResult;

    public sealed record Rejected(SubmitRejection Kind, string Error) : SubmitActionResult;
}
