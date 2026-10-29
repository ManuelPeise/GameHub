using Logic.Sessions.Connections;
using Shared.Models.Interfaces;

namespace Services.Api.Hubs;

/// <summary>
/// Removes players whose reconnect deadline or turn time limit has passed. Deadlines come from the stored disconnect time,
/// so they survive a restart; after a start, seats of users without connection get a new deadline.
/// </summary>
internal sealed class ConnectionWatchdog(
    ConnectionTracker tracker,
    IServiceScopeFactory scopeFactory,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    ILogger<ConnectionWatchdog> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunAsync(
            (connections, ct) => connections.MarkOfflinePlayersDisconnectedAsync(tracker.IsOnline, ct),
            "Marking offline players after start failed.",
            stoppingToken);

        using var timer = new PeriodicTimer(appSettings.Game.ConnectionCheckInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAsync(
                    (connections, ct) => connections.RemoveExpiredAsync(ct),
                    "Removing players after the reconnect deadline failed.",
                    stoppingToken);
                await RunAsync(
                    (connections, ct) => connections.RemoveTurnTimeoutsAsync(ct),
                    "Removing players after the turn time limit failed.",
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
    }

    private async Task RunAsync(Func<PlayerConnectionLogic, CancellationToken, Task> work, string error, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await work(scope.ServiceProvider.GetRequiredService<PlayerConnectionLogic>(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The database may not be reachable yet; the next tick tries again.
            logger.LogError(exception, "{Error}", error);
        }
    }
}
