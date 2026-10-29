using Shared.Models.Games;

namespace Logic.Shared.Models;

/// <summary>Seat of a session as the game logic needs it. <see cref="Order"/> is the seat index of the game module.</summary>
public sealed record SeatSnapshot(
    Guid Id,
    int Order,
    SeatKind Kind,
    Guid? UserId,
    SeatConnectionStatus ConnectionStatus,
    string? AiDifficulty,
    string? AiStrategyVersion);

/// <summary>Loaded state of a session including its seats and the last sequence of the move log.</summary>
public sealed record SessionSnapshot(
    Guid Id,
    string GameType,
    GameMode Mode,
    SessionStatus Status,
    Guid? CurrentSeatId,
    string StateJson,
    int StateVersion,
    long RngSeed,
    int LastSequence,
    IReadOnlyList<SeatSnapshot> Seats);

/// <summary>One entry of the append-only move log.</summary>
public sealed record GameActionRecord(
    Guid SessionId,
    Guid? SeatId,
    int Sequence,
    string ActionType,
    string? PayloadJson,
    string? RandomResultJson,
    string? AiStrategyVersion,
    DateTimeOffset CreatedAt);

/// <summary>Result of a seat that is written when the session finishes.</summary>
public sealed record SeatResultWrite(Guid SeatId, GameOutcome Outcome, int? Placement, int? Score);

/// <summary>
/// Everything one applied action changes, written in one transaction. Fails if the stored
/// state version differs from <see cref="ExpectedVersion"/>.
/// </summary>
public sealed record ActionWrite(
    Guid SessionId,
    int ExpectedVersion,
    GameActionRecord Action,
    string NewStateJson,
    SessionStatus NewStatus,
    Guid? CurrentSeatId,
    IReadOnlyList<SeatResultWrite> Results);

/// <summary>Event after an applied action (sent to the clients with US-013).</summary>
public sealed record GameActionApplied(
    Guid SessionId,
    int Sequence,
    int StateVersion,
    Guid? SeatId,
    string ActionType,
    string StateJson,
    bool IsFinished);

/// <summary>Event before an AI action delay starts.</summary>
public sealed record AiThinking(Guid SessionId, Guid SeatId);
