using Logic.Core.Model;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Sessions;

/// <summary>
/// Pausing, resuming and giving up singleplayer games. Pause and resume are written as system actions
/// (Paused, Resumed) to the move log.
/// </summary>
public sealed class SingleplayerPauseLogic(
    IGameSessionRepository repository,
    IGameEventPublisher publisher,
    IAiTurnScheduler scheduler,
    TimeProvider timeProvider)
{
    private const int MaxAttempts = 3;

    private static readonly SessionStatus[] RunningOnly = [SessionStatus.Running];

    /// <summary>The player pauses the game; only possible while it is the player's turn.</summary>
    public async Task<SessionLifecycleOutcome> PauseAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var (session, seat, failure) = await LoadOwnSingleplayerAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (session!.Status != SessionStatus.Running)
            {
                return Fail(SessionLifecycleFailureKind.InvalidState, "Die Partie läuft nicht.");
            }

            if (session.CurrentSeatId != seat!.Id)
            {
                return Fail(SessionLifecycleFailureKind.InvalidState, "Du kannst nur pausieren, wenn du am Zug bist.");
            }

            if (await WriteAsync(session, seat, SystemActionTypes.Paused, SessionStatus.Paused, cancellationToken).ConfigureAwait(false))
            {
                return await ReloadAsync(sessionId, seat.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        return Fail(SessionLifecycleFailureKind.Conflict, "Die Partie wurde parallel geändert. Bitte versuche es erneut.");
    }

    /// <summary>Lost connection: running singleplayer games of the user are paused, whoever is at turn.</summary>
    public async Task PauseOnDisconnectAsync(Guid userId, CancellationToken cancellationToken)
    {
        foreach (var sessionId in await repository.GetSessionIdsForUserAsync(userId, RunningOnly, cancellationToken).ConfigureAwait(false))
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
                var seat = session?.Seats.FirstOrDefault(candidate => candidate.UserId == userId);
                if (session is null || seat is null || session.Mode != GameMode.Singleplayer || session.Status != SessionStatus.Running)
                {
                    break;
                }

                if (await WriteAsync(session, seat, SystemActionTypes.Paused, SessionStatus.Paused, cancellationToken).ConfigureAwait(false))
                {
                    break;
                }
            }
        }
    }

    /// <summary>Continues a paused game; if the AI is at turn, it moves right away.</summary>
    public async Task<SessionLifecycleOutcome> ResumeAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var (session, seat, failure) = await LoadOwnSingleplayerAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (session!.Status == SessionStatus.Running)
            {
                return new SessionLifecycleOutcome.Success(session, seat!.Id);
            }

            if (session.Status != SessionStatus.Paused)
            {
                return Fail(SessionLifecycleFailureKind.InvalidState, "Die Partie ist nicht pausiert.");
            }

            if (!await WriteAsync(session, seat!, SystemActionTypes.Resumed, SessionStatus.Running, cancellationToken).ConfigureAwait(false))
            {
                continue;
            }

            if (session.CurrentSeatId is { } current && session.Seats.Any(s => s.Id == current && s.Kind == SeatKind.Ai))
            {
                await scheduler.EnqueueAsync(session.Id, cancellationToken).ConfigureAwait(false);
            }

            return await ReloadAsync(sessionId, seat!.Id, cancellationToken).ConfigureAwait(false);
        }

        return Fail(SessionLifecycleFailureKind.Conflict, "Die Partie wurde parallel geändert. Bitte versuche es erneut.");
    }

    /// <summary>Gives up an open singleplayer game (status Aborted), e.g. to start a new one.</summary>
    public async Task<SessionLifecycleOutcome> AbandonAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var (session, seat, failure) = await LoadOwnSingleplayerAsync(userId, sessionId, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                return failure;
            }

            if (session!.Status is not (SessionStatus.Running or SessionStatus.Paused))
            {
                return Fail(SessionLifecycleFailureKind.InvalidState, "Die Partie ist bereits beendet.");
            }

            if (await repository.SetStatusAsync(session.Id, session.StateVersion, SessionStatus.Aborted, timeProvider.GetUtcNow(), cancellationToken)
                    .ConfigureAwait(false))
            {
                return await ReloadAsync(sessionId, seat!.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        return Fail(SessionLifecycleFailureKind.Conflict, "Die Partie wurde parallel geändert. Bitte versuche es erneut.");
    }

    private async Task<bool> WriteAsync(
        SessionSnapshot session,
        SeatSnapshot seat,
        string actionType,
        SessionStatus newStatus,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var write = new ActionWrite(
            session.Id,
            session.StateVersion,
            new GameActionRecord(session.Id, seat.Id, session.LastSequence + 1, actionType, null, null, null, now),
            session.StateJson,
            newStatus,
            session.CurrentSeatId,
            [],
            TurnStartedAt: newStatus == SessionStatus.Running ? now : null);

        if (!await repository.SaveActionAsync(write, cancellationToken).ConfigureAwait(false))
        {
            return false;
        }

        await publisher.PublishSessionUpdatedAsync(new SessionUpdated(session.Id, session.StateVersion + 1, newStatus), cancellationToken)
            .ConfigureAwait(false);
        return true;
    }

    private async Task<(SessionSnapshot? Session, SeatSnapshot? Seat, SessionLifecycleOutcome.Failure? Failure)> LoadOwnSingleplayerAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return (null, null, Fail(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden."));
        }

        var seat = session.Seats.FirstOrDefault(candidate => candidate.UserId == userId);
        if (seat is null)
        {
            return (null, null, Fail(SessionLifecycleFailureKind.Forbidden, "Du bist kein Teilnehmer dieser Partie."));
        }

        if (session.Mode != GameMode.Singleplayer)
        {
            return (null, null, Fail(SessionLifecycleFailureKind.InvalidRequest, "Nur Singleplayer-Partien können pausiert werden."));
        }

        return (session, seat, null);
    }

    private async Task<SessionLifecycleOutcome> ReloadAsync(Guid sessionId, Guid seatId, CancellationToken cancellationToken)
    {
        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return session is null
            ? Fail(SessionLifecycleFailureKind.NotFound, "Die Partie wurde nicht gefunden.")
            : new SessionLifecycleOutcome.Success(session, seatId);
    }

    private static SessionLifecycleOutcome.Failure Fail(SessionLifecycleFailureKind kind, string error) => new(kind, error);
}
