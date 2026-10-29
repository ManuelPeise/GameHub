using Shared.Models.Games;

namespace Data.Database.Entities;

/// <summary>A player position in a session: a human with account or an AI opponent without account.</summary>
public sealed class Seat
{
    public Guid Id { get; set; }

    public Guid SessionId { get; set; }

    public GameSession? Session { get; set; }

    /// <summary>Position in the turn order, starting at 0.</summary>
    public int Order { get; set; }

    public SeatKind Kind { get; set; }

    /// <summary>Account of the human player; required for humans, empty for AI.</summary>
    public Guid? UserId { get; set; }

    /// <summary>Difficulty of the AI (e.g. Easy, Medium, Hard); required for AI, empty for humans.</summary>
    public string? AiDifficulty { get; set; }

    /// <summary>Version of the AI strategy at the start of the session; required for AI.</summary>
    public string? AiStrategyVersion { get; set; }

    public SeatConnectionStatus ConnectionStatus { get; set; }

    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>Name shown in the game, e.g. the display name or "KI (Schwer)".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Game-specific mark of the seat, e.g. "X" or "O" in Tic-Tac-Toe.</summary>
    public string? Symbol { get; set; }
}
