using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.AspNetCore.SignalR;
using Shared.Models.Realtime;

namespace Services.Api.Hubs;

/// <summary>Sends game events to the participants of a session via <see cref="GameSessionHub"/>.</summary>
public sealed class SignalRGameEventPublisher(IHubContext<GameSessionHub> hub) : IGameEventPublisher
{
    // Tic-Tac-Toe and Kniffel have no hidden information, so every participant gets the same state.
    // Games with hidden information need a per-seat view here.
    public Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(GameSessionHub.GroupName(gameEvent.SessionId)).SendAsync(
            GameHubEvents.ActionApplied,
            new ActionAppliedMessage(
                gameEvent.SessionId,
                gameEvent.Sequence,
                gameEvent.StateVersion,
                gameEvent.SeatId,
                gameEvent.ActionType,
                gameEvent.StateJson,
                gameEvent.IsFinished),
            cancellationToken);

    public Task PublishAiThinkingAsync(AiThinking gameEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(GameSessionHub.GroupName(gameEvent.SessionId)).SendAsync(
            GameHubEvents.AiThinking,
            new AiThinkingMessage(gameEvent.SessionId, gameEvent.SeatId),
            cancellationToken);

    public Task PublishRematchCreatedAsync(RematchCreated gameEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(GameSessionHub.GroupName(gameEvent.PreviousSessionId)).SendAsync(
            GameHubEvents.RematchCreated,
            new RematchCreatedMessage(gameEvent.PreviousSessionId, gameEvent.NewSessionId),
            cancellationToken);

    public Task PublishSessionUpdatedAsync(SessionUpdated gameEvent, CancellationToken cancellationToken) =>
        hub.Clients.Group(GameSessionHub.GroupName(gameEvent.SessionId)).SendAsync(
            GameHubEvents.SessionUpdated,
            new SessionUpdatedMessage(gameEvent.SessionId, gameEvent.StateVersion, gameEvent.Status),
            cancellationToken);
}
