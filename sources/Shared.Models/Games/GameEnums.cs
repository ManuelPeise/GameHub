namespace Shared.Models.Games;

/// <summary>Known game types (stored as text so that new games need no schema change).</summary>
public static class GameTypes
{
    public const string TicTacToe = "TicTacToe";
    public const string Kniffel = "Kniffel";
}

public enum GameMode
{
    Singleplayer,
    Multiplayer,
}

public enum SessionStatus
{
    /// <summary>Multiplayer session waiting for players.</summary>
    Lobby,
    Running,

    /// <summary>Singleplayer session paused by the player.</summary>
    Paused,
    Finished,
    Aborted,
}

public enum SeatKind
{
    Human,

    /// <summary>AI opponent; has no user account.</summary>
    Ai,
}

public enum SeatConnectionStatus
{
    Connected,
    Disconnected,

    /// <summary>Removed after failed reconnects or the turn timeout; skipped and not scored.</summary>
    Removed,
}

public enum GameOutcome
{
    Win,
    Loss,
    Draw,

    /// <summary>Player was removed during the game; no placement.</summary>
    Removed,
}
