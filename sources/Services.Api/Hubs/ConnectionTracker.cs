using System.Collections.Concurrent;

namespace Services.Api.Hubs;

/// <summary>Number of open hub connections per user (several tabs count as one presence).</summary>
public sealed class ConnectionTracker
{
    private readonly ConcurrentDictionary<Guid, int> _connections = new();

    /// <summary>Returns the number of connections after adding one.</summary>
    public int Add(Guid userId) => _connections.AddOrUpdate(userId, 1, (_, count) => count + 1);

    /// <summary>Returns the number of connections after removing one.</summary>
    public int Remove(Guid userId)
    {
        var remaining = _connections.AddOrUpdate(userId, 0, (_, count) => Math.Max(0, count - 1));
        if (remaining == 0)
        {
            _connections.TryRemove(new KeyValuePair<Guid, int>(userId, 0));
        }

        return remaining;
    }

    public bool IsOnline(Guid userId) => _connections.TryGetValue(userId, out var count) && count > 0;
}
