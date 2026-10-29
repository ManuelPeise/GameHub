using Logic.Sessions;

namespace Services.Api.Games;

/// <summary>Aborts open sessions without activity for Game.PausedSessionRetention; runs at start and hourly.</summary>
internal sealed class SessionCleanupJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<SessionCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);
        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<SessionMaintenanceLogic>().AbortInactiveAsync(stoppingToken);
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception, "Aborting inactive sessions failed.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
    }
}
