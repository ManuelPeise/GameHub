using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Sessions.History;
using Logic.Shared.Models;
using Logic.TicTacToe;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Games;

namespace Tests.Sessions;

public sealed class PauseHistoryAndMaintenanceTests
{
    private readonly InMemoryGameSessionRepository _repository = new();
    private readonly AiTurnRunnerTests.RecordingGameEventPublisher _publisher = new();
    private readonly AiTurnRunnerTests.TestAppSettings _settings = new(TimeSpan.Zero);
    private readonly AiTurnScheduler _scheduler = new();
    private readonly GameModuleRegistry _modules = new([new TicTacToeModule()]);
    private readonly SingleplayerPauseLogic _pauses;
    private readonly SubmitActionHandler _handler;

    private readonly Guid _user = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _humanSeat = Guid.NewGuid();
    private readonly Guid _aiSeat = Guid.NewGuid();

    public PauseHistoryAndMaintenanceTests()
    {
        _pauses = new SingleplayerPauseLogic(_repository, _publisher, _scheduler, TimeProvider.System);
        _handler = new SubmitActionHandler(_repository, _modules, _publisher, TimeProvider.System);
        _repository.Add(new SessionSnapshot(
            _sessionId,
            GameTypes.TicTacToe,
            GameMode.Singleplayer,
            SessionStatus.Running,
            _user,
            null,
            _humanSeat,
            _modules.Get(GameTypes.TicTacToe).CreateInitialState(2, SeededRandom.ForAction(9, 0)),
            0,
            9,
            0,
            [
                new SeatSnapshot(_humanSeat, 0, SeatKind.Human, "Alice", "X", _user, SeatConnectionStatus.Connected, null, null),
                new SeatSnapshot(_aiSeat, 1, SeatKind.Ai, "KI (Easy)", "O", null, SeatConnectionStatus.Connected, "Easy", "1"),
            ]));
    }

    private SessionSnapshot Session => _repository.Get(_sessionId);

    private Task<SubmitActionResult> Place(Guid seat, int cell) =>
        _handler.HandleAsync(new SubmitActionCommand(_sessionId, seat, null, "PlaceMark", $"{{\"cell\":{cell}}}"), CancellationToken.None);

    [Xunit.Fact]
    public async Task Pause_OnOwnTurn_IsLogged_AndBlocksMoves()
    {
        var paused = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _pauses.PauseAsync(_user, _sessionId, CancellationToken.None));

        Xunit.Assert.Equal(SessionStatus.Paused, paused.Session.Status);
        Xunit.Assert.Equal(SystemActionTypes.Paused, Xunit.Assert.Single(_repository.Actions).ActionType);
        var move = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(_humanSeat, 4));
        Xunit.Assert.Equal(SubmitRejection.NotRunning, move.Kind);
    }

    [Xunit.Fact]
    public async Task Pause_WhenAiIsAtTurn_IsRejected()
    {
        await Place(_humanSeat, 4); // AI is at turn now

        var result = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(await _pauses.PauseAsync(_user, _sessionId, CancellationToken.None));

        Xunit.Assert.Equal(SessionLifecycleFailureKind.InvalidState, result.Kind);
    }

    [Xunit.Fact]
    public async Task PauseOnDisconnect_PausesEvenWhenAiIsAtTurn()
    {
        await Place(_humanSeat, 4);

        await _pauses.PauseOnDisconnectAsync(_user, CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Paused, Session.Status);
    }

    [Xunit.Fact]
    public async Task Resume_IsLogged_AndAiMovesImmediatelyIfAtTurn()
    {
        await Place(_humanSeat, 4);
        await _pauses.PauseOnDisconnectAsync(_user, CancellationToken.None);

        var resumed = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _pauses.ResumeAsync(_user, _sessionId, CancellationToken.None));

        Xunit.Assert.Equal(SessionStatus.Running, resumed.Session.Status);
        Xunit.Assert.Equal(SystemActionTypes.Resumed, _repository.Actions[^1].ActionType);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var queue = _scheduler.DequeueAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        Xunit.Assert.True(await queue.MoveNextAsync());
        Xunit.Assert.Equal(_sessionId, queue.Current);
    }

    [Xunit.Fact]
    public async Task Abandon_AbortsTheGame_SoANewOneCanStart()
    {
        var result = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _pauses.AbandonAsync(_user, _sessionId, CancellationToken.None));

        Xunit.Assert.Equal(SessionStatus.Aborted, result.Session.Status);
    }

    [Xunit.Fact]
    public async Task OtherUser_CannotPauseOrResume()
    {
        var result = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(await _pauses.PauseAsync(Guid.NewGuid(), _sessionId, CancellationToken.None));

        Xunit.Assert.Equal(SessionLifecycleFailureKind.Forbidden, result.Kind);
    }

    [Xunit.Fact]
    public async Task InactiveSessions_AreAbortedAfterRetention()
    {
        _repository.LastActivity[_sessionId] = DateTimeOffset.UtcNow - TimeSpan.FromDays(31);
        var maintenance = new SessionMaintenanceLogic(_repository, _publisher, _settings, TimeProvider.System, NullLogger<SessionMaintenanceLogic>.Instance);

        Xunit.Assert.Equal(1, await maintenance.AbortInactiveAsync(CancellationToken.None));
        Xunit.Assert.Equal(SessionStatus.Aborted, Session.Status);
    }

    [Xunit.Fact]
    public async Task RecentSessions_StayOpen()
    {
        _repository.LastActivity[_sessionId] = DateTimeOffset.UtcNow - TimeSpan.FromDays(29);
        var maintenance = new SessionMaintenanceLogic(_repository, _publisher, _settings, TimeProvider.System, NullLogger<SessionMaintenanceLogic>.Instance);

        Xunit.Assert.Equal(0, await maintenance.AbortInactiveAsync(CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task History_ShowsFinishedGame_WithStepsAndSystemEvents()
    {
        // X: 0, 1, 2 wins; O: 3, 4 – with a pause in between
        await Place(_humanSeat, 0);
        await Place(_aiSeat, 3);
        await _pauses.PauseAsync(_user, _sessionId, CancellationToken.None);
        await _pauses.ResumeAsync(_user, _sessionId, CancellationToken.None);
        await Place(_humanSeat, 1);
        await Place(_aiSeat, 4);
        await Place(_humanSeat, 2);

        var history = new GameHistoryLogic(_repository, new ReplayService(_modules));
        var entry = Xunit.Assert.Single(await history.ListAsync(_user, 0, 20, CancellationToken.None));
        Xunit.Assert.Equal(GameOutcome.Win, entry.MyOutcome);
        Xunit.Assert.Equal(1, entry.MyPlacement);
        Xunit.Assert.Contains(entry.Seats, seat => seat.Kind == SeatKind.Ai && !seat.IsMe);

        var detail = await history.GetDetailAsync(_user, _sessionId, CancellationToken.None);
        Xunit.Assert.NotNull(detail);
        Xunit.Assert.Equal(7, detail.Actions.Count);
        Xunit.Assert.Contains(detail.Actions, action => action.ActionType == SystemActionTypes.Paused);
        Xunit.Assert.Contains("\"board\":\"---------\"", detail.InitialStateJson, StringComparison.Ordinal);
        Xunit.Assert.Equal(Session.StateJson, detail.Actions[^1].StateJson);
        Xunit.Assert.Equal(detail.Actions[1].StateJson, detail.Actions[2].StateJson); // pause keeps the state
    }

    [Xunit.Fact]
    public async Task History_OfOthersOrOpenGames_IsNotVisible()
    {
        var history = new GameHistoryLogic(_repository, new ReplayService(_modules));

        Xunit.Assert.Null(await history.GetDetailAsync(_user, _sessionId, CancellationToken.None)); // still running
        Xunit.Assert.Empty(await history.ListAsync(_user, 0, 20, CancellationToken.None));

        await _pauses.AbandonAsync(_user, _sessionId, CancellationToken.None);
        Xunit.Assert.Null(await history.GetDetailAsync(Guid.NewGuid(), _sessionId, CancellationToken.None));
        Xunit.Assert.NotNull(await history.GetDetailAsync(_user, _sessionId, CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task Admin_SeesEveryGame_AlsoRunningOnes_AndCanSearchByPlayer()
    {
        await Place(_humanSeat, 4);
        var history = new GameHistoryLogic(_repository, new ReplayService(_modules));

        var detail = await history.GetDetailForAdminAsync(_sessionId, CancellationToken.None);
        Xunit.Assert.NotNull(detail);
        Xunit.Assert.Single(detail.Actions);
        Xunit.Assert.All(detail.Entry.Seats, seat => Xunit.Assert.False(seat.IsMe));

        var found = await history.SearchForAdminAsync("alice", 0, 10, CancellationToken.None);
        Xunit.Assert.Equal(_sessionId, Xunit.Assert.Single(found).SessionId);
        Xunit.Assert.Contains("Alice", found[0].Players);
        Xunit.Assert.Empty(await history.SearchForAdminAsync("niemand", 0, 10, CancellationToken.None));
    }
}
