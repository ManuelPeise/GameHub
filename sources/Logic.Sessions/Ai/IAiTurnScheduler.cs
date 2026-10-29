namespace Logic.Sessions.Ai;

public interface IAiTurnScheduler
{
    ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken);

    IAsyncEnumerable<Guid> DequeueAsync(CancellationToken cancellationToken);
}
