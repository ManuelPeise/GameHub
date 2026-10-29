namespace Shared.Models.Games;

public sealed record CreateSessionRequest(
    string GameType,
    GameMode Mode,
    string? Difficulty,
    int? PreferredSeat);

public sealed record JoinSessionRequest(string InviteCode);

public sealed record SubmitSessionActionRequest(int? ExpectedVersion, string ActionType, string? Payload);

public sealed record SeatDto(
    Guid Id,
    int Order,
    SeatKind Kind,
    string DisplayName,
    string? Symbol,
    Guid? UserId,
    SeatConnectionStatus ConnectionStatus);

public sealed record SessionDto(
    Guid Id,
    string GameType,
    GameMode Mode,
    SessionStatus Status,
    int StateVersion,
    string StateJson,
    IReadOnlyList<SeatDto> Seats,
    string? InviteCode,
    Guid? MySeatId,
    Guid? CurrentSeatId,
    DateTimeOffset? TurnDeadline);

/// <summary>Owner adds an AI seat to a multiplayer lobby.</summary>
public sealed record AddAiSeatRequest(string Difficulty);

/// <summary>Entry of the public list of open lobbies.</summary>
public sealed record OpenLobbyDto(
    Guid Id,
    string GameType,
    string OwnerName,
    int SeatCount,
    int MaxSeats,
    bool IsMine,
    DateTimeOffset CreatedAt);

/// <summary>Seat of a session in the game history, with its result.</summary>
public sealed record HistorySeatDto(
    Guid SeatId,
    int Order,
    string DisplayName,
    SeatKind Kind,
    bool IsMe,
    GameOutcome? Outcome,
    int? Placement,
    int? Score);

/// <summary>Entry of the list of own finished and aborted games.</summary>
public sealed record GameHistoryEntryDto(
    Guid SessionId,
    string GameType,
    GameMode Mode,
    SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    GameOutcome? MyOutcome,
    int? MyPlacement,
    IReadOnlyList<HistorySeatDto> Seats);

/// <summary>One entry of the move log with the game state after it (for the replay).</summary>
public sealed record HistoryActionDto(
    int Sequence,
    Guid? SeatId,
    string ActionType,
    string? PayloadJson,
    string? RandomResultJson,
    string? AiStrategyVersion,
    DateTimeOffset CreatedAt,
    string StateJson);

/// <summary>A finished game with its complete move log.</summary>
public sealed record GameHistoryDetailDto(
    GameHistoryEntryDto Entry,
    string InitialStateJson,
    IReadOnlyList<HistoryActionDto> Actions);
