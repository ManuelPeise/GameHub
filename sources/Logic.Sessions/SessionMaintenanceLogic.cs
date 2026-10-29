using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;
using Shared.Models.Games;
using Shared.Models.Interfaces;

namespace Logic.Sessions;

/// <summary>Housekeeping: open sessions without activity for <c>Game.PausedSessionRetention</c> are aborted.</summary>
public sealed class SessionMaintenanceLogic(
    IGameSessionRepository repository,
    IGameEventPublisher publisher,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    ILogger<SessionMaintenanceLogic> logger)
{
    public async Task<int> AbortInactiveAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cutoff = now - appSettings.Game.PausedSessionRetention;
        var aborted = 0;

        foreach (var sessionId in await repository.GetInactiveSessionIdsAsync(cutoff, cancellationToken).ConfigureAwait(false))
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session is null || session.Status is not (SessionStatus.Lobby or SessionStatus.Running or SessionStatus.Paused))
            {
                continue;
            }

            if (await repository.SetStatusAsync(session.Id, session.StateVersion, SessionStatus.Aborted, now, cancellationToken)
                    .ConfigureAwait(false))
            {
                aborted++;
                await publisher.PublishSessionUpdatedAsync(
                        new SessionUpdated(session.Id, session.StateVersion + 1, SessionStatus.Aborted),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        if (aborted > 0)
        {
            logger.LogInformation("{Count} inactive sessions aborted (no activity since {Cutoff}).", aborted, cutoff);
        }

        return aborted;
    }
}
