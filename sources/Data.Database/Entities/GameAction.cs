namespace Data.Database.Entities;

/// <summary>
/// One entry of the move log (table game."GameAction"): moves of humans and AI and system events
/// (Disconnected, Reconnected, PlayerRemoved, TurnTimeout, Paused, Resumed). Append-only: entries are
/// never changed or deleted (enforced in <see cref="GameDbContext"/>).
/// </summary>
public sealed class GameAction
{
    public long Id { get; set; }

    public Guid SessionId { get; set; }

    /// <summary>Acting seat; empty for system events of the session.</summary>
    public Guid? SeatId { get; set; }

    /// <summary>Position within the session, starting at 1, without gaps.</summary>
    public int Sequence { get; set; }

    public string ActionType { get; set; } = string.Empty;

    /// <summary>Action data as JSON (jsonb), e.g. the field or the held dice.</summary>
    public string? PayloadJson { get; set; }

    /// <summary>Result of random draws during this action as JSON (jsonb), e.g. the thrown dice.</summary>
    public string? RandomResultJson { get; set; }

    /// <summary>Strategy version for AI actions.</summary>
    public string? AiStrategyVersion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
