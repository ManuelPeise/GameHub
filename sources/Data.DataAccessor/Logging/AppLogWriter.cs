using Data.Database;
using Data.Database.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Data.DataAccessor.Logging;

/// <summary>Writes queued log entries to the database in batches.</summary>
internal sealed class AppLogWriter(AppLogQueue queue, IServiceScopeFactory scopeFactory) : BackgroundService
{
    private const int MaxBatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var batch = new List<AppLogEntry>(MaxBatchSize);

        try
        {
            while (await queue.Reader.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < MaxBatchSize && queue.Reader.TryRead(out var entry))
                {
                    batch.Add(entry);
                }

                await WriteAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
    }

    private async Task WriteAsync(List<AppLogEntry> batch, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<GameDbContext>();

            database.AppLog.AddRange(batch);
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not via ILogger: that would queue a new entry for every failed write.
            await Console.Error.WriteLineAsync(
                $"[AppLogWriter] {batch.Count} log entries could not be written to the database: {exception.Message}");
        }
    }
}
