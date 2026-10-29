using Logic.Core.Ai;
using Logic.Core.Abstractions;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Logic.TicTacToe;
using Logic.TicTacToe.Ai;
using Logic.TicTacToe.Domain;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Games;
using Shared.Models.Interfaces;
using Shared.Models.Settings;

namespace Tests.Sessions;

public sealed class AiTurnRunnerTests
{
    [Xunit.Fact]
    public async Task RunningSessionsWithAiTurn_AreReturnedForRestart()
    {
        var module = new TicTacToeModule();
        var repository = new InMemoryGameSessionRepository();
        var aiSeatId = Guid.NewGuid();
        var humanSeatId = Guid.NewGuid();

        repository.Add(new SessionSnapshot(
            Guid.NewGuid(),
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            aiSeatId,
            module.CreateInitialState(2, SeededRandom.ForAction(1, 0)),
            0,
            1,
            0,
            [
                new SeatSnapshot(aiSeatId, 0, SeatKind.Ai, "KI", "X", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
                new SeatSnapshot(Guid.NewGuid(), 1, SeatKind.Human, "Mensch", "O", Guid.NewGuid(), SeatConnectionStatus.Connected, null, null),
            ]));

        repository.Add(new SessionSnapshot(
            Guid.NewGuid(),
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            humanSeatId,
            module.CreateInitialState(2, SeededRandom.ForAction(2, 0)),
            0,
            2,
            0,
            [
                new SeatSnapshot(humanSeatId, 0, SeatKind.Human, "Mensch", "X", Guid.NewGuid(), SeatConnectionStatus.Connected, null, null),
                new SeatSnapshot(Guid.NewGuid(), 1, SeatKind.Ai, "KI", "O", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
            ]));

        var sessions = await repository.GetRunningSessionsWithAiTurnAsync(CancellationToken.None);

        Xunit.Assert.Single(sessions);
    }

    [Xunit.Fact]
    public async Task HumanMove_QueuesSession_AndAiResponds()
    {
        var sessionId = Guid.NewGuid();
        var seatHuman = Guid.NewGuid();
        var seatAi = Guid.NewGuid();
        var module = new TicTacToeModule();
        var modules = new GameModuleRegistry([module]);
        var repository = new InMemoryGameSessionRepository();
        repository.Add(new SessionSnapshot(
            sessionId,
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            seatHuman,
            module.CreateInitialState(2, SeededRandom.ForAction(123, 0)),
            0,
            123,
            0,
            [
                new SeatSnapshot(seatHuman, 0, SeatKind.Human, "Mensch", "X", Guid.NewGuid(), SeatConnectionStatus.Connected, null, null),
                new SeatSnapshot(seatAi, 1, SeatKind.Ai, "KI", "O", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
            ]));

        var scheduler = new AiTurnScheduler();
        var publisher = new RecordingGameEventPublisher();
        var submitHandler = new SubmitActionHandler(repository, modules, publisher, TimeProvider.System, scheduler);
        var registry = new AiStrategyRegistry([new TicTacToeAiPlayer(module, new RandomTicTacToeStrategy())]);
        var runner = new AiTurnRunner(
            repository,
            submitHandler,
            modules,
            registry,
            publisher,
            new TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<AiTurnRunner>.Instance);

        var humanMove = await submitHandler.HandleAsync(
            new SubmitActionCommand(sessionId, seatHuman, 0, PlaceMark.ActionType, "{\"cell\":4}"),
            CancellationToken.None);
        Xunit.Assert.IsType<SubmitActionResult.Applied>(humanMove);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Guid? queuedSession = null;
        await foreach (var candidate in scheduler.DequeueAsync(timeout.Token))
        {
            queuedSession = candidate;
            break;
        }

        Xunit.Assert.True(queuedSession.HasValue);
        Xunit.Assert.Equal(sessionId, queuedSession!.Value);

        await runner.RunSessionAsync(sessionId, throwOnInvalidAction: false, CancellationToken.None);

        Xunit.Assert.Equal(2, repository.Actions.Count);
        Xunit.Assert.Equal(seatAi, repository.Actions[1].SeatId);
        Xunit.Assert.Single(publisher.AiThinkingEvents);
    }

    [Xunit.Fact]
    public async Task TwoAiSeats_FinishGameAutomatically()
    {
        var sessionId = Guid.NewGuid();
        var seatA = Guid.NewGuid();
        var seatB = Guid.NewGuid();
        var module = new TicTacToeModule();
        var modules = new GameModuleRegistry([module]);
        var repository = new InMemoryGameSessionRepository();
        repository.Add(new SessionSnapshot(
            sessionId,
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            seatA,
            module.CreateInitialState(2, SeededRandom.ForAction(55, 0)),
            0,
            55,
            0,
            [
                new SeatSnapshot(seatA, 0, SeatKind.Ai, "KI A", "X", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
                new SeatSnapshot(seatB, 1, SeatKind.Ai, "KI B", "O", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
            ]));

        var publisher = new RecordingGameEventPublisher();
        var runner = new AiTurnRunner(
            repository,
            new SubmitActionHandler(repository, modules, publisher, TimeProvider.System),
            modules,
            new AiStrategyRegistry([new TicTacToeAiPlayer(module, new RandomTicTacToeStrategy())]),
            publisher,
            new TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<AiTurnRunner>.Instance);

        await runner.RunSessionAsync(sessionId, throwOnInvalidAction: false, CancellationToken.None);

        var session = repository.Get(sessionId);
        Xunit.Assert.Equal(SessionStatus.Finished, session.Status);
        Xunit.Assert.True(repository.Actions.Count is >= 5 and <= 9);
        Xunit.Assert.NotEmpty(publisher.AiThinkingEvents);
    }

    [Xunit.Fact]
    public async Task InvalidAiAction_ThrowsInTestMode()
    {
        var sessionId = Guid.NewGuid();
        var seatAi = Guid.NewGuid();
        var module = new TicTacToeModule();
        var modules = new GameModuleRegistry([module]);
        var repository = new InMemoryGameSessionRepository();
        repository.Add(new SessionSnapshot(
            sessionId,
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            seatAi,
            module.CreateInitialState(2, SeededRandom.ForAction(11, 0)),
            0,
            11,
            0,
            [new SeatSnapshot(seatAi, 0, SeatKind.Ai, "KI", "X", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "broken")]));

        var runner = new AiTurnRunner(
            repository,
            new SubmitActionHandler(repository, modules, new RecordingGameEventPublisher(), TimeProvider.System),
            modules,
            new AiStrategyRegistry([new InvalidTicTacToeAiPlayer()]),
            new RecordingGameEventPublisher(),
            new TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<AiTurnRunner>.Instance);

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.RunSessionAsync(sessionId, throwOnInvalidAction: true, CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task Replay_AfterAiMoves_RebuildsStoredState()
    {
        var sessionId = Guid.NewGuid();
        var seatA = Guid.NewGuid();
        var seatB = Guid.NewGuid();
        var module = new TicTacToeModule();
        var modules = new GameModuleRegistry([module]);
        var repository = new InMemoryGameSessionRepository();
        repository.Add(new SessionSnapshot(
            sessionId,
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            seatA,
            module.CreateInitialState(2, SeededRandom.ForAction(77, 0)),
            0,
            77,
            0,
            [
                new SeatSnapshot(seatA, 0, SeatKind.Ai, "KI A", "X", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
                new SeatSnapshot(seatB, 1, SeatKind.Ai, "KI B", "O", null, SeatConnectionStatus.Connected, AiDifficulties.Easy, "1"),
            ]));

        var publisher = new RecordingGameEventPublisher();
        var runner = new AiTurnRunner(
            repository,
            new SubmitActionHandler(repository, modules, publisher, TimeProvider.System),
            modules,
            new AiStrategyRegistry([new TicTacToeAiPlayer(module, new RandomTicTacToeStrategy())]),
            publisher,
            new TestAppSettings(TimeSpan.Zero),
            TimeProvider.System,
            NullLogger<AiTurnRunner>.Instance);

        await runner.RunSessionAsync(sessionId, throwOnInvalidAction: false, CancellationToken.None);

        var session = repository.Get(sessionId);
        var actions = await repository.GetActionsAsync(sessionId, CancellationToken.None);
        var replayedState = new ReplayService(modules).Replay(session, actions);

        Xunit.Assert.Equal(session.StateJson, replayedState);
    }

    internal sealed class RecordingGameEventPublisher : IGameEventPublisher
    {
        public List<AiThinking> AiThinkingEvents { get; } = [];

        public List<SessionUpdated> SessionUpdatedEvents { get; } = [];

        public Task PublishSessionUpdatedAsync(SessionUpdated gameEvent, CancellationToken cancellationToken)
        {
            SessionUpdatedEvents.Add(gameEvent);
            return Task.CompletedTask;
        }

        public Task PublishAsync(GameActionApplied gameEvent, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task PublishAiThinkingAsync(AiThinking gameEvent, CancellationToken cancellationToken)
        {
            AiThinkingEvents.Add(gameEvent);
            return Task.CompletedTask;
        }
    }

    internal sealed class TestAppSettings(TimeSpan aiDelay) : IAppSettings
    {
        public GameSettings Game { get; } = new();

        public AiSettings Ai { get; } = new() { ActionDelay = aiDelay };

        public EmailSettings Email { get; } = new();

        public BackupSettings Backup { get; } = new();

        public LoggingSettings Logging { get; } = new();

        public SecuritySettings Security { get; } = new();

        public EnvironmentSettings Environment { get; } = new();
    }

    private sealed class InvalidTicTacToeAiPlayer : IAiPlayer
    {
        public string GameType => TicTacToeModule.Type;

        public string Difficulty => AiDifficulties.Easy;

        public string Version => "broken";

        public (string ActionType, string? PayloadJson) ChooseAction(string stateJson, int seat, IRandom random) =>
            (PlaceMark.ActionType, "{\"cell\":99}");
    }
}
