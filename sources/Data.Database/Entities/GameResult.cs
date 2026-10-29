using Shared.Models.Games;

namespace Data.Database.Entities;

/// <summary>Result of one seat after the end of a session.</summary>
public sealed class GameResult
{
    public Guid SessionId { get; set; }

    public Guid SeatId { get; set; }

    /// <summary>1 = winner; empty for removed players.</summary>
    public int? Placement { get; set; }

    /// <summary>Points (Kniffel); empty for games without points.</summary>
    public int? Score { get; set; }

    public GameOutcome Outcome { get; set; }
}
