using System.Text.Json;
using Logic.Core.Ai;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Kniffel;
using Logic.Kniffel.Ai;
using Logic.Kniffel.Domain;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Sessions.Connections;
using Logic.Shared.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Games;

namespace Tests.Sessions;

public sealed class KniffelSessionTests
{
    private readonly InMemoryGameSessionRepository _repository = new();
    private readonly AiTurnRunnerTests.RecordingGameEventPublisher _publisher = new();
    private readonly KniffelModule _module = new();
    private readonly GameModuleRegistry _modules;
    private readonly SubmitActionHandler _handler;

    public KniffelSessionTests()
    {
        _modules = new GameModuleRegistry([_module]);
        _handler = new SubmitActionHandler(_repository, _modules, _publisher, TimeProvider.System);
    }

    private SessionSnapshot AddSession(GameMode mode, params SeatSnapshot[] seats)
    {
        var session = new SessionSnapshot(
            Guid.NewGuid(),
            GameTypes.Kniffel,
            mode,
            SessionStatus.Running,
            seats[0].UserId ?? Guid.NewGuid(),
            null,
            seats[0].Id,
            _module.CreateInitialState(seats.Length, SeededRandom.ForAction(77, 0)),
            0,
            77,
            0,
            seats);
        _repository.Add(session);
        return session;
    }

    private static SeatSnapshot Human(int order, string name) =>
        new(Guid.NewGuid(), order, SeatKind.Human, name, null, Guid.NewGuid(), SeatConnectionStatus.Connected, null, null);

    private static SeatSnapshot Ai(int order, string difficulty) =>
        new(Guid.NewGuid(), order, SeatKind.Ai, $"KI ({difficulty})", null, null, SeatConnectionStatus.Connected, difficulty, "1");

    /// <summary>The human plays a simple turn: roll once, score the first legal category.</summary>
    private async Task HumanTurnAsync(Guid sessionId, SeatSnapshot seat)
    {
        var (rollType, rollPayload) = _module.SerializeAction(new Roll(0));
        Xunit.Assert.IsType<SubmitActionResult.Applied>(await _handler.HandleAsync(
            new SubmitActionCommand(sessionId, seat.Id, null, rollType, rollPayload), CancellationToken.None));

        var state = _module.DeserializeState(_repository.Get(sessionId).StateJson);
        var category = KniffelScoring.LegalCategories(state.Sheets[seat.Order], state.Dice)[0];
        var (scoreType, scorePayload) = _module.SerializeAction(new Score(category));
        Xunit.Assert.IsType<SubmitActionResult.Applied>(await _handler.HandleAsync(
            new SubmitActionCommand(sessionId, seat.Id, null, scoreType, scorePayload), CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task Singleplayer_AgainstHardAi_PlaysToTheEnd_WithLoggedDice_AndReplayMatches()
    {
        var human = Human(0, "Alice");
        var session = AddSession(GameMode.Singleplayer, human, Ai(1, AiDifficulties.Hard));
        var runner = new AiTurnRunner(
            _repository,
            _handler,
            _modules,
            new AiStrategyRegistry([new KniffelAiPlayer(_module, KniffelAiStrategy.Hard)]),
            _publisher,
            new AiTurnRunnerTests.TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<AiTurnRunner>.Instance);

        while (_repository.Get(session.Id).Status == SessionStatus.Running)
        {
            await HumanTurnAsync(session.Id, human);
            await runner.RunSessionAsync(session.Id, throwOnInvalidAction: true, CancellationToken.None);
        }

        var stored = _repository.Get(session.Id);
        Xunit.Assert.Equal(SessionStatus.Finished, stored.Status);
        Xunit.Assert.Equal(2, _repository.Results.Count);
        Xunit.Assert.All(_repository.Results, result => Xunit.Assert.NotNull(result.Score));

        // Each roll logs its dice; the AI acts in single actions (one log entry per roll or score).
        var actions = await _repository.GetActionsAsync(session.Id, CancellationToken.None);
        Xunit.Assert.All(actions.Where(a => a.ActionType == Roll.ActionType), a => Xunit.Assert.NotNull(a.RandomResultJson));
        Xunit.Assert.Equal(26, actions.Count(a => a.ActionType == Score.ActionType));
        Xunit.Assert.Contains(_publisher.AiThinkingEvents, e => e.SessionId == session.Id);

        Xunit.Assert.Equal(stored.StateJson, new ReplayService(_modules).Replay(stored, actions));
    }

    [Xunit.Fact]
    public async Task Multiplayer_RemovedPlayer_IsStruckOff_OthersPlayOn_AndPlacementsAreStored()
    {
        var alice = Human(0, "Alice");
        var bob = Human(1, "Bob");
        var carla = Human(2, "Carla");
        var session = AddSession(GameMode.Multiplayer, alice, bob, carla);
        var connections = new PlayerConnectionLogic(
            _repository,
            _modules,
            _publisher,
            new AiTurnScheduler(),
            new AiTurnRunnerTests.TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<PlayerConnectionLogic>.Instance);

        await HumanTurnAsync(session.Id, alice);
        await connections.RemovePlayerAsync(session.Id, bob.Id, RemovalReasons.TurnTimeout, CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(session.Id).Status);
        Xunit.Assert.Equal(carla.Id, _repository.Get(session.Id).CurrentSeatId);

        while (_repository.Get(session.Id).Status == SessionStatus.Running)
        {
            var current = _repository.Get(session.Id).CurrentSeatId;
            await HumanTurnAsync(session.Id, current == alice.Id ? alice : carla);
        }

        var results = _repository.Results;
        Xunit.Assert.Contains(new SeatResultWrite(bob.Id, GameOutcome.Removed, null, null), results);
        Xunit.Assert.Contains(results, result => result.SeatId != bob.Id && result.Placement == 1);
        Xunit.Assert.Equal(3, results.Count);

        var stored = _repository.Get(session.Id);
        var state = _module.DeserializeState(stored.StateJson);
        Xunit.Assert.True(state.Removed[1]);
        Xunit.Assert.Equal(stored.StateJson, new ReplayService(_modules).Replay(stored, await _repository.GetActionsAsync(session.Id, CancellationToken.None)));
    }

    [Xunit.Fact]
    public async Task Multiplayer_LastRemainingPlayer_Wins()
    {
        var alice = Human(0, "Alice");
        var bob = Human(1, "Bob");
        var session = AddSession(GameMode.Multiplayer, alice, bob);
        var connections = new PlayerConnectionLogic(
            _repository, _modules, _publisher, new AiTurnScheduler(),
            new AiTurnRunnerTests.TestAppSettings(TimeSpan.Zero), TimeProvider.System, NullLogger<PlayerConnectionLogic>.Instance);

        await connections.RemovePlayerAsync(session.Id, alice.Id, RemovalReasons.ReconnectTimeout, CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Finished, _repository.Get(session.Id).Status);
        Xunit.Assert.Contains(new SeatResultWrite(bob.Id, GameOutcome.Win, 1, null), _repository.Results);
    }

    [Xunit.Fact]
    public void RecordedDice_MatchTheState()
    {
        var json = _module.CreateInitialState(2, SeededRandom.ForAction(5, 0));
        var random = new RecordingRandom(SeededRandom.ForAction(5, 1));
        var applied = Xunit.Assert.IsType<ActionResult.Applied>(_module.Apply(json, 0, Roll.ActionType, "{\"heldMask\":0}", random));

        var dice = _module.DeserializeState(applied.StateJson).Dice;
        Xunit.Assert.Equal(dice.Select(d => d - 1).ToArray(), random.Draws.ToArray());
        Xunit.Assert.Equal(5, JsonSerializer.Deserialize<int[]>(JsonSerializer.Serialize(random.Draws))!.Length);
    }
}
