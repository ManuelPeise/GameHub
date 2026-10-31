namespace Shared.Models.Administration;

/// <summary>One entry of the application log.</summary>
public sealed record AppLogEntryDto(
    long Id,
    DateTimeOffset Timestamp,
    string Level,
    string Category,
    string Message,
    string? Exception,
    int? EventId,
    string? Properties);

/// <summary>
/// A page of the application log, newest first. Load the next page with <c>beforeId = NextBeforeId</c>;
/// null means there are no older entries.
/// </summary>
public sealed record AppLogPageDto(IReadOnlyList<AppLogEntryDto> Items, long? NextBeforeId);

/// <summary>One entry of the audit log; <see cref="UserName"/> is the current display name of the user.</summary>
public sealed record AuditEntryDto(
    long Id,
    DateTimeOffset CreatedAt,
    Guid? UserId,
    string? UserName,
    string Action,
    string? Target,
    string? TargetName,
    string Result,
    string? Details);

public sealed record AuditPageDto(IReadOnlyList<AuditEntryDto> Items, long? NextBeforeId);

/// <summary>A game in the admin list (all players, all games except lobbies).</summary>
public sealed record AdminGameDto(
    Guid SessionId,
    string GameType,
    Games.GameMode Mode,
    Games.SessionStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    IReadOnlyList<string> Players);
