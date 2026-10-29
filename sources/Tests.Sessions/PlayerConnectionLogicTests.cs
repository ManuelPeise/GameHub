using System.Text.Json;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Sessions.Connections;
using Logic.Shared.Models;
using Logic.TicTacToe;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Games;

namespace Tests.Sessions;

public sealed class PlayerConnectionLogicTests
{
    private readonly InMemoryGameSessionRepository _repository = new();
    private readonly AiTurnRunnerTests.RecordingGameEventPublisher _publisher = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
    private readonly AiTurnRunnerTests.TestAppSettings _settings = new(TimeSpan.Zero);
    private readonly GameModuleRegistry _modules = new([new TicTacToeModule()]);
    private readonly PlayerConnectionLogic _logic;

    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _seatA = Guid.NewGuid();
    private readonly Guid _seatB = Guid.NewGuid();

    public PlayerConnectionLogicTests()
    {
        _logic = new PlayerConnectionLogic(
            _repository,
            _modules,
            _publisher,
            new AiTurnScheduler(),
            _settings,
            _time,
            NullLogger<PlayerConnectionLogic>.Instance);

        _repository.Add(Session(_sessionId, GameMode.Multiplayer, _userA, _seatA, _userB, _seatB) with { TurnStartedAt = _time.GetUtcNow() });
    }

    private SessionSnapshot Session(Guid id, GameMode mode, Guid userA, Guid seatA, Guid? userB, Guid seatB, SessionStatus status = SessionStatus.Running) =>
        new(
            id,
            GameTypes.TicTacToe,
            mode,
            status,
            userA,
            null,
            seatA,
            _modules.Get(GameTypes.TicTacToe).CreateInitialState(2, SeededRandom.ForAction(5, 0)),
            0,
            5,
            0,
            [
                new SeatSnapshot(seatA, 0, SeatKind.Human, "Alice", "X", userA, SeatConnectionStatus.Connected, null, null),
                userB is { } b
                    ? new SeatSnapshot(seatB, 1, SeatKind.Human, "Bob", "O", b, SeatConnectionStatus.Connected, null, null)
                    : new SeatSnapshot(seatB, 1, SeatKind.Ai, "KI", "O", null, SeatConnectionStatus.Connected, "Easy", "1"),
            ]);

    private SeatSnapshot SeatB => _repository.Get(_sessionId).Seats.Single(seat => seat.Id == _seatB);

    [Xunit.Fact]
    public async Task Disconnect_MarksSeat_LogsAndNotifies()
    {
        await _logic.UserDisconnectedAsync(_userB, CancellationToken.None);

        Xunit.Assert.Equal(SeatConnectionStatus.Disconnected, SeatB.ConnectionStatus);
        var action = Xunit.Assert.Single(_repository.Actions);
        Xunit.Assert.Equal(SystemActionTypes.Disconnected, action.ActionType);
        Xunit.Assert.Equal(_seatB, action.SeatId);
        Xunit.Assert.Equal(_time.GetUtcNow(), _repository.DisconnectedAt[_seatB]);
        Xunit.Assert.Single(_publisher.SessionUpdatedEvents);
    }

    [Xunit.Fact]
    public async Task Reconnect_MarksSeatConnected_AndLogs()
    {
        await _logic.UserDisconnectedAsync(_userB, CancellationToken.None);
        await _logic.UserConnectedAsync(_userB, CancellationToken.None);

        Xunit.Assert.Equal(SeatConnectionStatus.Connected, SeatB.ConnectionStatus);
        Xunit.Assert.Equal(
            new[] { SystemActionTypes.Disconnected, SystemActionTypes.Reconnected },
            _repository.Actions.Select(action => action.ActionType));
        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(_sessionId).Status);
    }

    [Xunit.Fact]
    public async Task ConnectedAgain_WithoutDisconnect_WritesNothing()
    {
        await _logic.UserConnectedAsync(_userB, CancellationToken.None);

        Xunit.Assert.Empty(_repository.Actions);
    }

    [Xunit.Fact]
    public async Task BeforeDeadline_NobodyIsRemoved()
    {
        await _logic.UserDisconnectedAsync(_userB, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(59));

        await _logic.RemoveExpiredAsync(CancellationToken.None);

        Xunit.Assert.Equal(SeatConnectionStatus.Disconnected, SeatB.ConnectionStatus);
        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(_sessionId).Status);
    }

    [Xunit.Fact]
    public async Task AfterDeadline_PlayerIsRemoved_AndLastPlayerWins()
    {
        await _logic.UserDisconnectedAsync(_userB, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(60));

        await _logic.RemoveExpiredAsync(CancellationToken.None);

        var session = _repository.Get(_sessionId);
        Xunit.Assert.Equal(SessionStatus.Finished, session.Status);
        Xunit.Assert.Equal(SeatConnectionStatus.Removed, SeatB.ConnectionStatus);
        var removal = _repository.Actions[^1];
        Xunit.Assert.Equal(SystemActionTypes.PlayerRemoved, removal.ActionType);
        Xunit.Assert.Equal(RemovalReasons.ReconnectTimeout, JsonDocument.Parse(removal.PayloadJson!).RootElement.GetProperty("reason").GetString());
        Xunit.Assert.Contains(new SeatResultWrite(_seatA, GameOutcome.Win, 1, null), _repository.Results);
        Xunit.Assert.Contains(new SeatResultWrite(_seatB, GameOutcome.Removed, null, null), _repository.Results);
    }

    [Xunit.Fact]
    public async Task RemovedPlayer_CannotMoveAnymore_AndReconnectChangesNothing()
    {
        await _logic.RemovePlayerAsync(_sessionId, _seatB, RemovalReasons.TurnTimeout, CancellationToken.None);
        var count = _repository.Actions.Count;

        await _logic.UserConnectedAsync(_userB, CancellationToken.None);

        Xunit.Assert.Equal(count, _repository.Actions.Count);
        Xunit.Assert.Equal(SeatConnectionStatus.Removed, SeatB.ConnectionStatus);
    }

    [Xunit.Fact]
    public async Task AfterRestart_OfflinePlayersGetADeadline_OnlinePlayersStay()
    {
        await _logic.MarkOfflinePlayersDisconnectedAsync(userId => userId == _userA, CancellationToken.None);

        var seats = _repository.Get(_sessionId).Seats;
        Xunit.Assert.Equal(SeatConnectionStatus.Connected, seats.Single(seat => seat.Id == _seatA).ConnectionStatus);
        Xunit.Assert.Equal(SeatConnectionStatus.Disconnected, SeatB.ConnectionStatus);
    }

    [Xunit.Fact]
    public async Task Replay_WithSystemActions_EqualsStoredState()
    {
        var handler = new SubmitActionHandler(_repository, _modules, _publisher, TimeProvider.System);
        await handler.HandleAsync(new SubmitActionCommand(_sessionId, _seatA, null, "PlaceMark", "{\"cell\":4}"), CancellationToken.None);
        await _logic.UserDisconnectedAsync(_userB, CancellationToken.None);
        await _logic.UserConnectedAsync(_userB, CancellationToken.None);
        await handler.HandleAsync(new SubmitActionCommand(_sessionId, _seatB, null, "PlaceMark", "{\"cell\":0}"), CancellationToken.None);
        await _logic.RemovePlayerAsync(_sessionId, _seatB, RemovalReasons.ReconnectTimeout, CancellationToken.None);

        var session = _repository.Get(_sessionId);
        var replayed = new ReplayService(_modules).Replay(session, await _repository.GetActionsAsync(_sessionId, CancellationToken.None));

        Xunit.Assert.Equal(session.StateJson, replayed);
        Xunit.Assert.Equal(Enumerable.Range(1, 5), _repository.Actions.Select(action => action.Sequence));
    }

    [Xunit.Fact]
    public async Task AccountDeletion_RemovesFromMultiplayer_AndAbortsSingleplayerAndOwnLobby()
    {
        var singleId = Guid.NewGuid();
        _repository.Add(Session(singleId, GameMode.Singleplayer, _userB, Guid.NewGuid(), null, Guid.NewGuid()));
        var lobbyId = Guid.NewGuid();
        _repository.Add(Session(lobbyId, GameMode.Multiplayer, _userB, Guid.NewGuid(), null, Guid.NewGuid(), SessionStatus.Lobby));

        await _logic.RemoveUserFromOpenSessionsAsync(_userB, CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Finished, _repository.Get(_sessionId).Status);
        Xunit.Assert.Contains(new SeatResultWrite(_seatA, GameOutcome.Win, 1, null), _repository.Results);
        Xunit.Assert.Equal(SessionStatus.Aborted, _repository.Get(singleId).Status);
        Xunit.Assert.Equal(SessionStatus.Aborted, _repository.Get(lobbyId).Status);
    }

    [Xunit.Fact]
    public async Task AccountDeletion_FreesTheSeatInSomeoneElsesLobby()
    {
        var lobbyId = Guid.NewGuid();
        _repository.Add(Session(lobbyId, GameMode.Multiplayer, _userA, Guid.NewGuid(), _userB, Guid.NewGuid(), SessionStatus.Lobby));

        await _logic.RemoveUserFromOpenSessionsAsync(_userB, CancellationToken.None);

        var lobby = _repository.Get(lobbyId);
        Xunit.Assert.Equal(SessionStatus.Lobby, lobby.Status);
        Xunit.Assert.Single(lobby.Seats);
    }

    [Xunit.Fact]
    public async Task TurnTimeout_BeforeTwoMinutes_DoesNothing()
    {
        _time.Advance(TimeSpan.FromSeconds(119));

        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);

        Xunit.Assert.Empty(_repository.Actions);
    }

    [Xunit.Fact]
    public async Task TurnTimeout_LogsTimeoutAndRemoval_OtherPlayerWins()
    {
        _time.Advance(TimeSpan.FromMinutes(2));

        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);

        Xunit.Assert.Equal(
            new[] { SystemActionTypes.TurnTimeout, SystemActionTypes.PlayerRemoved },
            _repository.Actions.Select(action => action.ActionType));
        Xunit.Assert.All(_repository.Actions, action => Xunit.Assert.Equal(_seatA, action.SeatId));
        Xunit.Assert.Equal(SessionStatus.Finished, _repository.Get(_sessionId).Status);
        Xunit.Assert.Contains(new SeatResultWrite(_seatB, GameOutcome.Win, 1, null), _repository.Results);
        Xunit.Assert.Contains(new SeatResultWrite(_seatA, GameOutcome.Removed, null, null), _repository.Results);
    }

    [Xunit.Fact]
    public async Task OwnMove_RestartsTheTimer_ForTheNextSeat()
    {
        var handler = new SubmitActionHandler(_repository, _modules, _publisher, _time);
        _time.Advance(TimeSpan.FromSeconds(90));
        await handler.HandleAsync(new SubmitActionCommand(_sessionId, _seatA, null, "PlaceMark", "{\"cell\":4}"), CancellationToken.None);

        _time.Advance(TimeSpan.FromSeconds(60)); // 2:30 since start, 1:00 since B's turn began
        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);
        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(_sessionId).Status);

        _time.Advance(TimeSpan.FromSeconds(60));
        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);
        Xunit.Assert.Equal(SeatConnectionStatus.Removed, SeatB.ConnectionStatus);
    }

    [Xunit.Fact]
    public async Task Singleplayer_HasNoTurnTimeout()
    {
        var singleId = Guid.NewGuid();
        var humanSeat = Guid.NewGuid();
        _repository.Add(Session(singleId, GameMode.Singleplayer, Guid.NewGuid(), humanSeat, null, Guid.NewGuid()) with { TurnStartedAt = _time.GetUtcNow() });
        _time.Advance(TimeSpan.FromHours(1));

        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(singleId).Status);
        Xunit.Assert.DoesNotContain(_repository.Actions, action => action.SessionId == singleId);
    }

    [Xunit.Fact]
    public async Task DisconnectedPlayerAtTurn_IsHandledByTheReconnectDeadline_NotTheTurnLimit()
    {
        _time.Advance(TimeSpan.FromSeconds(100));
        await _logic.UserDisconnectedAsync(_userA, CancellationToken.None);
        _time.Advance(TimeSpan.FromSeconds(30)); // turn over time, reconnect deadline not yet

        await _logic.RemoveTurnTimeoutsAsync(CancellationToken.None);

        Xunit.Assert.Equal(SessionStatus.Running, _repository.Get(_sessionId).Status);
        Xunit.Assert.DoesNotContain(_repository.Actions, action => action.ActionType == SystemActionTypes.TurnTimeout);
    }

    private sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
