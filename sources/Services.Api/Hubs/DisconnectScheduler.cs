using Logic.Sessions;
using Logic.Sessions.Connections;
using Shared.Models.Interfaces;

namespace Services.Api.Hubs;

/// <summary>
/// Marks a user as disconnected only if no connection came back within the grace period, so page changes
/// and short hiccups do not end up in the move log.
/// </summary>
public sealed class DisconnectScheduler(
    ConnectionTracker tracker,
    IServiceScopeFactory scopeFactory,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    IHostApplicationLifetime lifetime,
    ILogger<DisconnectScheduler> logger)
{
    public void Schedule(Guid userId) => _ = RunAsync(userId, lifetime.ApplicationStopping);

    private async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            if (appSettings.Game.DisconnectGracePeriod > TimeSpan.Zero)
            {
                await Task.Delay(appSettings.Game.DisconnectGracePeriod, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            if (tracker.IsOnline(userId))
            {
                return;
            }

            await using var scope = scopeFactory.CreateAsyncScope();
            var connections = scope.ServiceProvider.GetRequiredService<PlayerConnectionLogic>();
            await connections.UserDisconnectedAsync(userId, cancellationToken).ConfigureAwait(false);

            // Singleplayer: a lost connection pauses the game (US-016).
            var pauses = scope.ServiceProvider.GetRequiredService<SingleplayerPauseLogic>();
            await pauses.PauseOnDisconnectAsync(userId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Application is stopping; the watchdog marks open seats after the next start.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Marking user {UserId} as disconnected failed.", userId);
        }
    }
}
