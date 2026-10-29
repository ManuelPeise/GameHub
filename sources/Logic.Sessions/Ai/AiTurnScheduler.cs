using System.Threading.Channels;

namespace Logic.Sessions.Ai;

public sealed class AiTurnScheduler : IAiTurnScheduler
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
    {
        SingleReader = true,
    });

    public ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken) =>
        _queue.Writer.WriteAsync(sessionId, cancellationToken);

    public IAsyncEnumerable<Guid> DequeueAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
