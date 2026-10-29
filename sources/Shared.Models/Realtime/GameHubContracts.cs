using Shared.Models.Games;

namespace Shared.Models.Realtime;

/// <summary>Address of the SignalR hub for game sessions.</summary>
public static class GameHubRoutes
{
    public const string Path = "/hubs/game";
}

/// <summary>Hub methods the client invokes.</summary>
public static class GameHubMethods
{
    /// <summary>Subscribe to the events of a session; returns false if the user is not a participant.</summary>
    public const string JoinSession = "JoinSession";

    public const string LeaveSession = "LeaveSession";
}

/// <summary>Events the server sends to the clients of a session.</summary>
public static class GameHubEvents
{
    public const string ActionApplied = "ActionApplied";
    public const string AiThinking = "AiThinking";
    public const string SessionUpdated = "SessionUpdated";
    public const string RematchCreated = "RematchCreated";
}

/// <summary>A move was stored; contains the action and the new state visible to the receiver.</summary>
public sealed record ActionAppliedMessage(
    Guid SessionId,
    int Sequence,
    int StateVersion,
    Guid? SeatId,
    string ActionType,
    string StateJson,
    bool IsFinished);

/// <summary>An AI seat is about to move ("KI denkt …").</summary>
public sealed record AiThinkingMessage(Guid SessionId, Guid SeatId);

/// <summary>Seats or status of a session changed outside of a move (lobby join, AI seat, start).</summary>
public sealed record SessionUpdatedMessage(Guid SessionId, int StateVersion, SessionStatus Status);

/// <summary>A new round was started from this (finished) session.</summary>
public sealed record RematchCreatedMessage(Guid SessionId, Guid NewSessionId);
