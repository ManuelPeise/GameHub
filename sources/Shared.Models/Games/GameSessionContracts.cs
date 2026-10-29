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
