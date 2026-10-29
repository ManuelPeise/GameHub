using Data.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Models.Interfaces;

namespace Data.DataAccessor.Logging;

/// <summary>Deletes application log entries older than <c>App:Logging:Retention</c>, at start and then daily.</summary>
internal sealed class AppLogCleanupJob(
    IServiceScopeFactory scopeFactory,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    ILogger<AppLogCleanupJob> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, timeProvider);

        try
        {
            do
            {
                await CleanUpAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
    }

    private async Task CleanUpAsync(CancellationToken cancellationToken)
    {
        try
        {
            var cutoff = timeProvider.GetUtcNow() - appSettings.Logging.Retention;

            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            var deleted = await database.AppLog
                .Where(entry => entry.Timestamp < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} application log entries older than {Cutoff}.", deleted, cutoff);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Cleaning up the application log failed.");
        }
    }
}
