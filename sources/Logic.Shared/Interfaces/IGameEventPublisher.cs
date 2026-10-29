using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for informing the players of a session about changes (SignalR with US-013).</summary>
public interface IGameEventPublisher
{
    Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken);
}
