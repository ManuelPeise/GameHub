using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Services.Api.Games;

internal sealed class AiTurnWorker(
    IAiTurnScheduler scheduler,
    IServiceScopeFactory scopeFactory,
    ILogger<AiTurnWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ResumeRunningSessionsAsync(stoppingToken);

        try
        {
            await foreach (var sessionId in scheduler.DequeueAsync(stoppingToken))
            {
                await ProcessSessionAsync(sessionId, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application is stopping.
        }
    }

    private async Task ResumeRunningSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IGameSessionRepository>();
            var sessions = await repository.GetRunningSessionsWithAiTurnAsync(cancellationToken).ConfigureAwait(false);

            foreach (var sessionId in sessions)
            {
                await scheduler.EnqueueAsync(sessionId, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Do not stop the API if the database is not reachable yet; open AI turns resume with the next move.
            logger.LogError(exception, "Resuming AI turns after start failed.");
        }
    }

    private async Task ProcessSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var runner = scope.ServiceProvider.GetRequiredService<AiTurnRunner>();
            await runner.RunSessionAsync(sessionId, throwOnInvalidAction: false, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Processing AI turns failed for session {SessionId}.", sessionId);
        }
    }
}
