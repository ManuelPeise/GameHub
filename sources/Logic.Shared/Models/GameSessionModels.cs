using Shared.Models.Games;

namespace Logic.Shared.Models;

/// <summary>Seat of a session as the game logic needs it. <see cref="Order"/> is the seat index of the game module.</summary>
public sealed record SeatSnapshot(
    Guid Id,
    int Order,
    SeatKind Kind,
    string DisplayName,
    string? Symbol,
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
    Guid OwnerUserId,
    string? InviteCode,
    Guid? CurrentSeatId,
    string StateJson,
    int StateVersion,
    long RngSeed,
    int LastSequence,
    IReadOnlyList<SeatSnapshot> Seats);

/// <summary>Seat values used when creating or extending a session.</summary>
public sealed record SeatCreateWrite(
    Guid Id,
    int Order,
    SeatKind Kind,
    string DisplayName,
    string? Symbol,
    Guid? UserId,
    string? AiDifficulty,
    string? AiStrategyVersion,
    SeatConnectionStatus ConnectionStatus,
    DateTimeOffset? DisconnectedAt);

/// <summary>All values for creating a session and its initial seats in one transaction.</summary>
public sealed record SessionCreateWrite(
    Guid Id,
    string GameType,
    GameMode Mode,
    SessionStatus Status,
    Guid OwnerUserId,
    string? InviteCode,
    Guid? CurrentSeatId,
    string StateJson,
    int StateVersion,
    long RngSeed,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SeatCreateWrite> Seats);

/// <summary>Optimistic-lock write for adding one seat in a lobby.</summary>
public sealed record SeatAddWrite(
    Guid SessionId,
    int ExpectedVersion,
    SeatCreateWrite Seat,
    DateTimeOffset UpdatedAt);

/// <summary>Optimistic-lock write for switching a lobby to running state.</summary>
public sealed record SessionStartWrite(
    Guid SessionId,
    int ExpectedVersion,
    Guid? CurrentSeatId,
    string StateJson,
    DateTimeOffset UpdatedAt);

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

/// <summary>New position (and symbol) of a remaining seat after a lobby seat was removed.</summary>
public sealed record SeatReorder(Guid SeatId, int Order, string? Symbol);

/// <summary>Optimistic-lock write for removing one seat from a lobby; later seats move up.</summary>
public sealed record SeatRemoveWrite(
    Guid SessionId,
    int ExpectedVersion,
    Guid SeatId,
    IReadOnlyList<SeatReorder> Reorders,
    DateTimeOffset UpdatedAt);

/// <summary>Open multiplayer lobby for the public list.</summary>
public sealed record OpenLobbySummary(
    Guid Id,
    string GameType,
    Guid OwnerUserId,
    string OwnerName,
    int SeatCount,
    DateTimeOffset CreatedAt);

/// <summary>Event after a change of seats or status outside of a move.</summary>
public sealed record SessionUpdated(Guid SessionId, int StateVersion, SessionStatus Status);
