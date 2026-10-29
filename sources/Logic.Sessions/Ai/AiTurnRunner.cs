using Logic.Core.Ai;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging;
using Shared.Models.Games;
using Shared.Models.Interfaces;

namespace Logic.Sessions.Ai;

public sealed class AiTurnRunner(
    IGameSessionRepository repository,
    SubmitActionHandler submitActionHandler,
    GameModuleRegistry modules,
    IAiStrategyRegistry strategies,
    IGameEventPublisher publisher,
    IAppSettings appSettings,
    TimeProvider timeProvider,
    ILogger<AiTurnRunner> logger)
{
    public async Task RunSessionAsync(Guid sessionId, bool throwOnInvalidAction, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session is null || session.Status != SessionStatus.Running)
            {
                return;
            }

            var module = modules.Get(session.GameType);
            var progress = module.GetProgress(session.StateJson);
            if (progress.IsFinished || progress.CurrentSeat is null)
            {
                return;
            }

            var seat = session.Seats.FirstOrDefault(candidate => candidate.Order == progress.CurrentSeat.Value);
            if (seat is null || seat.Kind != SeatKind.Ai)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(seat.AiDifficulty))
            {
                logger.LogWarning("AI seat {SeatId} in session {SessionId} has no difficulty.", seat.Id, session.Id);
                return;
            }

            await publisher.PublishAiThinkingAsync(new AiThinking(session.Id, seat.Id), cancellationToken).ConfigureAwait(false);
            if (appSettings.Ai.ActionDelay > TimeSpan.Zero)
            {
                await Task.Delay(appSettings.Ai.ActionDelay, timeProvider, cancellationToken).ConfigureAwait(false);
            }

            var nextSequence = session.LastSequence + 1;
            var random = SeededRandom.ForAiDecision(session.RngSeed, nextSequence);
            var player = strategies.Get(session.GameType, seat.AiDifficulty);
            var (actionType, payloadJson) = player.ChooseAction(session.StateJson, seat.Order, random);

            var preValidation = module.Apply(session.StateJson, seat.Order, actionType, payloadJson, random);
            if (preValidation is ActionResult.Rejected invalid)
            {
                if (throwOnInvalidAction)
                {
                    throw new InvalidOperationException(
                        $"AI strategy {player.Version} returned invalid action for session {session.Id}: {invalid.Error}");
                }

                logger.LogWarning(
                    "AI strategy returned invalid action in session {SessionId} (seat {SeatId}, game {GameType}, difficulty {Difficulty}): {Error}. Fallback to random legal action.",
                    session.Id,
                    seat.Id,
                    session.GameType,
                    seat.AiDifficulty,
                    invalid.Error);

                var legalActions = module.GetLegalActions(session.StateJson, seat.Order);
                if (legalActions.Count == 0)
                {
                    logger.LogWarning("No legal fallback action for AI seat {SeatId} in session {SessionId}.", seat.Id, session.Id);
                    return;
                }

                var fallbackIndex = random.Next(legalActions.Count);
                (actionType, payloadJson) = legalActions[fallbackIndex];
            }

            var result = await submitActionHandler.HandleAsync(
                    new SubmitActionCommand(
                        session.Id,
                        seat.Id,
                        session.StateVersion,
                        actionType,
                        payloadJson,
                        player.Version),
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SubmitActionResult.Applied)
            {
                continue;
            }

            var rejected = (SubmitActionResult.Rejected)result;
            if (rejected.Kind == SubmitRejection.Conflict)
            {
                continue;
            }

            if (throwOnInvalidAction && rejected.Kind == SubmitRejection.InvalidAction)
            {
                throw new InvalidOperationException($"AI action could not be applied in session {session.Id}: {rejected.Error}");
            }

            logger.LogWarning(
                "AI action rejected in session {SessionId} with {Kind}: {Error}",
                session.Id,
                rejected.Kind,
                rejected.Error);
            return;
        }
    }
}
