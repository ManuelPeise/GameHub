using Logic.Shared.Interfaces;
using Logic.Shared.Models;

namespace Logic.Sessions;

/// <summary>Publishes nothing; used until the real-time connection exists (US-013).</summary>
public sealed class NullGameEventPublisher : IGameEventPublisher
{
    public Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
