using System.Text.Json;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Sessions;

/// <summary>
/// The single path for every move of humans and AI: load, check turn and version, apply via the game
/// module with the session's deterministic random source, append to the move log, publish the event.
/// </summary>
public sealed class SubmitActionHandler(
    IGameSessionRepository repository,
    GameModuleRegistry modules,
    IGameEventPublisher publisher,
    TimeProvider timeProvider,
    IAiTurnScheduler? aiTurnScheduler = null)
{
    public async Task<SubmitActionResult> HandleAsync(SubmitActionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await repository.GetAsync(command.SessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return Reject(SubmitRejection.NotFound, "Die Partie wurde nicht gefunden.");
        }

        if (session.Status != SessionStatus.Running)
        {
            return Reject(SubmitRejection.NotRunning, "Die Partie läuft nicht.");
        }

        if (command.ExpectedVersion is { } expected && expected != session.StateVersion)
        {
            return Reject(SubmitRejection.Conflict, "Der Spielstand hat sich geändert. Bitte neu laden.");
        }

        var seat = session.Seats.FirstOrDefault(candidate => candidate.Id == command.SeatId);
        if (seat is null || seat.ConnectionStatus == SeatConnectionStatus.Removed)
        {
            return Reject(SubmitRejection.NotYourTurn, "Du bist nicht am Zug.");
        }

        if (SystemActionTypes.All.Contains(command.ActionType))
        {
            return Reject(SubmitRejection.InvalidAction, "Diese Aktion ist in diesem Spiel nicht möglich.");
        }

        var module = modules.Get(session.GameType);
        if (module.GetProgress(session.StateJson).CurrentSeat != seat.Order)
        {
            return Reject(SubmitRejection.NotYourTurn, "Du bist nicht am Zug.");
        }

        var sequence = session.LastSequence + 1;
        var random = new RecordingRandom(SeededRandom.ForAction(session.RngSeed, sequence));
        var outcome = module.Apply(session.StateJson, seat.Order, command.ActionType, command.PayloadJson, random);
        if (outcome is ActionResult.Rejected rejected)
        {
            return Reject(SubmitRejection.InvalidAction, rejected.Error);
        }

        var applied = (ActionResult.Applied)outcome;
        var progress = applied.Progress;
        var now = timeProvider.GetUtcNow();
        var action = new GameActionRecord(
            session.Id,
            seat.Id,
            sequence,
            command.ActionType,
            command.PayloadJson,
            random.Draws.Count > 0 ? JsonSerializer.Serialize(random.Draws) : null,
            seat.Kind == SeatKind.Ai ? command.AiStrategyVersion ?? seat.AiStrategyVersion : null,
            now);

        var write = new ActionWrite(
            session.Id,
            session.StateVersion,
            action,
            applied.StateJson,
            progress.IsFinished ? SessionStatus.Finished : SessionStatus.Running,
            progress.CurrentSeat is { } current ? session.Seats.FirstOrDefault(s => s.Order == current)?.Id : null,
            progress.IsFinished ? SessionResults.Map(session.Seats, progress) : [],
            TurnStartedAt: now); // every own action restarts the turn time limit (Kniffel: several actions per turn)

        if (!await repository.SaveActionAsync(write, cancellationToken).ConfigureAwait(false))
        {
            return Reject(SubmitRejection.Conflict, "Der Spielstand hat sich geändert. Bitte neu laden.");
        }

        var newVersion = session.StateVersion + 1;
        await publisher.PublishAsync(
            new GameActionApplied(session.Id, sequence, newVersion, seat.Id, command.ActionType, applied.StateJson, progress.IsFinished),
            cancellationToken).ConfigureAwait(false);

        if (!progress.IsFinished
            && progress.CurrentSeat is { } nextOrder
            && session.Seats.Any(nextSeat => nextSeat.Order == nextOrder && nextSeat.Kind == SeatKind.Ai)
            && aiTurnScheduler is not null)
        {
            await aiTurnScheduler.EnqueueAsync(session.Id, cancellationToken).ConfigureAwait(false);
        }

        return new SubmitActionResult.Applied(newVersion, sequence, applied.StateJson, progress);
    }

    private static SubmitActionResult.Rejected Reject(SubmitRejection kind, string error) => new(kind, error);
}
