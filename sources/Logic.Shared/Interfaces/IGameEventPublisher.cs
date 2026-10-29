using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for informing the players of a session about changes (SignalR).</summary>
public interface IGameEventPublisher
{
    Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken);

    Task PublishAiThinkingAsync(AiThinking gameEvent, CancellationToken cancellationToken);

    /// <summary>Seats or status changed outside of a move (lobby join, AI seat, start).</summary>
    Task PublishSessionUpdatedAsync(SessionUpdated gameEvent, CancellationToken cancellationToken);

    /// <summary>A new round was started from a finished session (sent to the clients of the old session).</summary>
    Task PublishRematchCreatedAsync(RematchCreated gameEvent, CancellationToken cancellationToken);
}
