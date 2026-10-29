using System.Threading.Channels;
using Data.Database.Entities;

namespace Data.DataAccessor.Logging;

/// <summary>
/// Buffer between the loggers and <see cref="AppLogWriter"/>. Logging never waits for the database;
/// if the buffer is full, the oldest entries are dropped (they are still in the console log).
/// </summary>
public sealed class AppLogQueue
{
    private const int Capacity = 10_000;

    private readonly Channel<AppLogEntry> _channel = Channel.CreateBounded<AppLogEntry>(
        new BoundedChannelOptions(Capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    internal ChannelReader<AppLogEntry> Reader => _channel.Reader;

    internal void Enqueue(AppLogEntry entry) => _channel.Writer.TryWrite(entry);
}
