using System.Text.Json;
using Logic.Core.Model;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;
using Shared.Models.Games;
using Shared.Models.Interfaces;

namespace Logic.Sessions.Connections;

/// <summary>Reasons stored in the payload of a PlayerRemoved entry.</summary>
public static class RemovalReasons
{
    public const string ReconnectTimeout = "ReconnectTimeout";
    public const string TurnTimeout = "TurnTimeout";
    public const string AccountDeleted = "AccountDeleted";
}

/// <summary>
/// Connection handling in multiplayer: marks seats as disconnected and reconnected, removes players
/// after the reconnect deadline and ends their participation when an account is deleted. Every change is
/// written as system action to the move log; deadlines are computed from the stored disconnect time.
/// </summary>
public sealed class PlayerConnectionLogic(
    IGameSessionRepository repository,
    GameModuleRegistry modules,
    IGameEventPublisher publisher,
    IAiTurnScheduler scheduler,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    ILogger<PlayerConnectionLogic> logger) : IPlayerSessionCleanup
{
    private const int MaxAttempts = 3;

    private static readonly SessionStatus[] RunningOnly = [SessionStatus.Running];
    private static readonly SessionStatus[] OpenStatuses = [SessionStatus.Lobby, SessionStatus.Running, SessionStatus.Paused];

    /// <summary>The user has no connection left: mark the seats in running multiplayer sessions.</summary>
    public async Task UserDisconnectedAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var sessionId in await repository.GetSessionIdsForUserAsync(userId, RunningOnly, cancellationToken).ConfigureAwait(false))
        {
            await SetConnectionAsync(sessionId, userId, SeatConnectionStatus.Disconnected, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The user is connected again: disconnected seats become connected.</summary>
    public async Task UserConnectedAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var sessionId in await repository.GetSessionIdsForUserAsync(userId, RunningOnly, cancellationToken).ConfigureAwait(false))
        {
            await SetConnectionAsync(sessionId, userId, SeatConnectionStatus.Connected, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// After a server start nobody is connected yet: seats still marked as connected whose user has no
    /// connection get the usual reconnect deadline from now on.
    /// </summary>
    public async Task MarkOfflinePlayersDisconnectedAsync(Func<Guid, bool> isOnline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(isOnline);

        var seats = await repository.GetMultiplayerSeatsAsync(SeatConnectionStatus.Connected, cancellationToken).ConfigureAwait(false);
        foreach (var seat in seats.Where(seat => !isOnline(seat.UserId)))
        {
            await SetConnectionAsync(seat.SessionId, seat.UserId, SeatConnectionStatus.Disconnected, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Removes all players whose reconnect deadline has passed.</summary>
    public async Task RemoveExpiredAsync(CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - appSettings.Game.ReconnectTimeout;
        var expired = await repository.GetExpiredDisconnectedSeatsAsync(cutoff, cancellationToken).ConfigureAwait(false);
        foreach (var seat in expired)
        {
            await RemovePlayerAsync(seat.SessionId, seat.SeatId, RemovalReasons.ReconnectTimeout, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Removes a seat from a running session: it is skipped and not scored. If only one active seat is
    /// left, the game ends and that seat wins.
    /// </summary>
    public async Task<bool> RemovePlayerAsync(Guid sessionId, Guid seatId, string reason, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var seat = session?.Seats.FirstOrDefault(candidate => candidate.Id == seatId);
            if (session is null || seat is null || session.Status != SessionStatus.Running
                || seat.ConnectionStatus == SeatConnectionStatus.Removed)
            {
                return false;
            }

            var module = modules.Get(session.GameType);
            var seatsAfter = session.Seats
                .Select(candidate => candidate.Id == seat.Id ? candidate with { ConnectionStatus = SeatConnectionStatus.Removed } : candidate)
                .ToList();
            var active = seatsAfter.Count(candidate => candidate.ConnectionStatus != SeatConnectionStatus.Removed);

            var state = module.RemoveSeat(session.StateJson, seat.Order);
            bool finished;
            Guid? currentSeatId = null;
            List<SeatResultWrite> results;
            if (active <= 1)
            {
                finished = true;
                results = SessionResults.LastSeatStanding(seatsAfter);
            }
            else
            {
                var progress = module.GetProgress(state);
                finished = progress.IsFinished;
                results = finished ? SessionResults.Map(seatsAfter, progress) : [];
                currentSeatId = progress.CurrentSeat is { } order ? seatsAfter.FirstOrDefault(s => s.Order == order)?.Id : null;
            }

            var now = timeProvider.GetUtcNow();
            var sequence = session.LastSequence + 1;
            var action = new GameActionRecord(
                session.Id,
                seat.Id,
                sequence,
                SystemActionTypes.PlayerRemoved,
                JsonSerializer.Serialize(new { reason }),
                null,
                null,
                now);
            var write = new ActionWrite(
                session.Id,
                session.StateVersion,
                action,
                state,
                finished ? SessionStatus.Finished : SessionStatus.Running,
                currentSeatId,
                results,
                [new SeatStatusChange(seat.Id, SeatConnectionStatus.Removed, null)]);

            if (!await repository.SaveActionAsync(write, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            logger.LogInformation(
                "Seat {SeatId} removed from session {SessionId} ({Reason}); finished: {Finished}.",
                seat.Id,
                session.Id,
                reason,
                finished);

            var version = session.StateVersion + 1;
            await publisher.PublishAsync(
                    new GameActionApplied(session.Id, sequence, version, seat.Id, SystemActionTypes.PlayerRemoved, state, finished),
                    cancellationToken)
                .ConfigureAwait(false);
            await publisher.PublishSessionUpdatedAsync(
                    new SessionUpdated(session.Id, version, finished ? SessionStatus.Finished : SessionStatus.Running),
                    cancellationToken)
                .ConfigureAwait(false);

            if (!finished && currentSeatId is { } next && seatsAfter.Any(s => s.Id == next && s.Kind == SeatKind.Ai))
            {
                await scheduler.EnqueueAsync(session.Id, cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        logger.LogWarning("Removing seat {SeatId} from session {SessionId} failed after repeated conflicts.", seatId, sessionId);
        return false;
    }

    /// <summary>
    /// Account deletion: running multiplayer → removed like after a lost connection; singleplayer and own
    /// lobbies → aborted; other lobbies → seat freed.
    /// </summary>
    public async Task RemoveUserFromOpenSessionsAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var sessionId in await repository.GetSessionIdsForUserAsync(userId, OpenStatuses, cancellationToken).ConfigureAwait(false))
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var seat = session?.Seats.FirstOrDefault(candidate => candidate.UserId == userId);
            if (session is null || seat is null)
            {
                continue;
            }

            if (session.Mode == GameMode.Multiplayer && session.Status == SessionStatus.Running)
            {
                await RemovePlayerAsync(session.Id, seat.Id, RemovalReasons.AccountDeleted, cancellationToken).ConfigureAwait(false);
            }
            else if (session.Status == SessionStatus.Lobby && session.OwnerUserId != userId)
            {
                await LeaveLobbyAsync(session, seat, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await AbortAsync(session, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task SetConnectionAsync(Guid sessionId, Guid userId, SeatConnectionStatus target, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session is null || session.Mode != GameMode.Multiplayer || session.Status != SessionStatus.Running)
            {
                return;
            }

            var seat = session.Seats.FirstOrDefault(candidate => candidate.UserId == userId && candidate.Kind == SeatKind.Human);
            if (seat is null || seat.ConnectionStatus == SeatConnectionStatus.Removed || seat.ConnectionStatus == target)
            {
                return;
            }

            var now = timeProvider.GetUtcNow();
            var actionType = target == SeatConnectionStatus.Disconnected ? SystemActionTypes.Disconnected : SystemActionTypes.Reconnected;
            var write = new ActionWrite(
                session.Id,
                session.StateVersion,
                new GameActionRecord(session.Id, seat.Id, session.LastSequence + 1, actionType, null, null, null, now),
                session.StateJson,
                session.Status,
                session.CurrentSeatId,
                [],
                [new SeatStatusChange(seat.Id, target, target == SeatConnectionStatus.Disconnected ? now : null)]);

            if (await repository.SaveActionAsync(write, cancellationToken).ConfigureAwait(false))
            {
                await publisher.PublishSessionUpdatedAsync(
                        new SessionUpdated(session.Id, session.StateVersion + 1, session.Status),
                        cancellationToken)
                    .ConfigureAwait(false);
                return;
            }
        }
    }

    private async Task LeaveLobbyAsync(SessionSnapshot session, SeatSnapshot seat, CancellationToken cancellationToken)
    {
        var reorders = session.Seats
            .Where(candidate => candidate.Order > seat.Order)
            .OrderBy(candidate => candidate.Order)
            .Select(candidate => new SeatReorder(candidate.Id, candidate.Order - 1, SeatSymbols.For(candidate.Order - 1)))
            .ToList();
        var now = timeProvider.GetUtcNow();
        if (await repository.RemoveSeatAsync(new SeatRemoveWrite(session.Id, session.StateVersion, seat.Id, reorders, now), cancellationToken)
                .ConfigureAwait(false))
        {
            await publisher.PublishSessionUpdatedAsync(new SessionUpdated(session.Id, session.StateVersion + 1, session.Status), cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task AbortAsync(SessionSnapshot session, CancellationToken cancellationToken)
    {
        if (await repository.SetStatusAsync(session.Id, session.StateVersion, SessionStatus.Aborted, timeProvider.GetUtcNow(), cancellationToken)
                .ConfigureAwait(false))
        {
            await publisher.PublishSessionUpdatedAsync(new SessionUpdated(session.Id, session.StateVersion + 1, SessionStatus.Aborted), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
