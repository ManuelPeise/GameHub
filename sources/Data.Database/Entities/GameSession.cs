using Shared.Models.Games;

namespace Data.Database.Entities;

/// <summary>One game (table game."GameSession"). The game-specific state is stored as JSON.</summary>
public sealed class GameSession
{
    public Guid Id { get; set; }

    /// <summary>Game type, see <see cref="GameTypes"/>.</summary>
    public string GameType { get; set; } = string.Empty;

    public GameMode Mode { get; set; }

    public SessionStatus Status { get; set; }

    /// <summary>Invite code for multiplayer lobbies; null for singleplayer and started rematches.</summary>
    public string? InviteCode { get; set; }

    /// <summary>
    /// User who created the session (the human player in singleplayer, the host in multiplayer).
    /// Used for the rule "at most one open singleplayer session per player and game type".
    /// </summary>
    public Guid OwnerUserId { get; set; }

    /// <summary>Seat whose turn it is; null before the start and after the end.</summary>
    public Guid? CurrentSeatId { get; set; }

    /// <summary>Game-specific state as JSON (jsonb), written by the game module.</summary>
    public string StateJson { get; set; } = "{}";

    /// <summary>Incremented with every action; optimistic concurrency token against double moves.</summary>
    public int StateVersion { get; set; }

    /// <summary>Seed of the random generator of this session (dice, AI choices); makes games reproducible.</summary>
    public long RngSeed { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Last action of a player; paused singleplayer sessions are aborted after 30 days of inactivity.</summary>
    public DateTimeOffset LastActivityAt { get; set; }

    public List<Seat> Seats { get; set; } = [];
}
