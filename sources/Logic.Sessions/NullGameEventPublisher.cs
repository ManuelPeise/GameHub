using Logic.Shared.Interfaces;
using Logic.Shared.Models;

namespace Logic.Sessions;

/// <summary>Publishes nothing; used in tests and tools without real-time connection.</summary>
public sealed class NullGameEventPublisher : IGameEventPublisher
{
    public Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishAiThinkingAsync(AiThinking gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishSessionUpdatedAsync(SessionUpdated gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishRematchCreatedAsync(RematchCreated gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
