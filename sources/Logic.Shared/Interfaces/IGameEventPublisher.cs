using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for informing the players of a session about changes (SignalR).</summary>
public interface IGameEventPublisher
{
    Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken);

    Task PublishAiThinkingAsync(AiThinking gameEvent, CancellationToken cancellationToken);

    /// <summary>Seats or status changed outside of a move (lobby join, AI seat, start).</summary>
    Task PublishSessionUpdatedAsync(SessionUpdated gameEvent, CancellationToken cancellationToken);
}
