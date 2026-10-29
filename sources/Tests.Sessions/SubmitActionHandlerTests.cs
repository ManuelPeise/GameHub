using Logic.Core.Abstractions;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Sessions;
using Logic.Shared.Models;
using Logic.TicTacToe;
using Shared.Models.Games;

namespace Tests.Sessions;

public sealed class SubmitActionHandlerTests
{
    private static readonly Guid SeatX = Guid.NewGuid();
    private static readonly Guid SeatO = Guid.NewGuid();

    private readonly InMemoryGameSessionRepository _repository = new();
    private readonly GameModuleRegistry _modules = new([new TicTacToeModule()]);
    private readonly SubmitActionHandler _handler;
    private readonly Guid _sessionId = Guid.NewGuid();

    public SubmitActionHandlerTests()
    {
        _handler = new SubmitActionHandler(_repository, _modules, new NullGameEventPublisher(), TimeProvider.System);
        const long seed = 42;
        _repository.Add(new SessionSnapshot(
            _sessionId,
            GameTypes.TicTacToe,
            GameMode.Multiplayer,
            SessionStatus.Running,
            Guid.NewGuid(),
            null,
            SeatX,
            _modules.Get(GameTypes.TicTacToe).CreateInitialState(2, SeededRandom.ForAction(seed, 0)),
            0,
            seed,
            0,
            [
                new SeatSnapshot(SeatX, 0, SeatKind.Human, "Spieler X", "X", Guid.NewGuid(), SeatConnectionStatus.Connected, null, null),
                new SeatSnapshot(SeatO, 1, SeatKind.Human, "Spieler O", "O", Guid.NewGuid(), SeatConnectionStatus.Connected, null, null),
            ]));
    }

    private Task<SubmitActionResult> Place(Guid seat, int cell, int? expectedVersion = null) =>
        _handler.HandleAsync(
            new SubmitActionCommand(_sessionId, seat, expectedVersion, "PlaceMark", $"{{\"cell\":{cell}}}"),
            CancellationToken.None);

    [Xunit.Fact]
    public async Task ValidMove_IsStoredWithSequenceAndVersion()
    {
        var result = Xunit.Assert.IsType<SubmitActionResult.Applied>(await Place(SeatX, 4, 0));

        Xunit.Assert.Equal(1, result.StateVersion);
        Xunit.Assert.Equal(1, result.Sequence);
        var action = Xunit.Assert.Single(_repository.Actions);
        Xunit.Assert.Equal(SeatX, action.SeatId);
        Xunit.Assert.Equal(1, action.Sequence);
        Xunit.Assert.Equal(1, _repository.Get(_sessionId).StateVersion);
    }

    [Xunit.Fact]
    public async Task InvalidMove_IsRejected_AndNothingIsStored()
    {
        await Place(SeatX, 4);

        var result = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(SeatO, 4));

        Xunit.Assert.Equal(SubmitRejection.InvalidAction, result.Kind);
        Xunit.Assert.Equal("Das Feld ist bereits belegt.", result.Error);
        Xunit.Assert.Single(_repository.Actions);
        Xunit.Assert.Equal(1, _repository.Get(_sessionId).StateVersion);
    }

    [Xunit.Fact]
    public async Task MoveOutOfTurn_IsRejected()
    {
        var result = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(SeatO, 0));

        Xunit.Assert.Equal(SubmitRejection.NotYourTurn, result.Kind);
        Xunit.Assert.Empty(_repository.Actions);
    }

    [Xunit.Fact]
    public async Task UnknownSeat_IsRejected()
    {
        var result = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(Guid.NewGuid(), 0));

        Xunit.Assert.Equal(SubmitRejection.NotYourTurn, result.Kind);
    }

    [Xunit.Fact]
    public async Task UnknownSession_IsRejected()
    {
        var result = await _handler.HandleAsync(
            new SubmitActionCommand(Guid.NewGuid(), SeatX, null, "PlaceMark", "{\"cell\":0}"), CancellationToken.None);

        Xunit.Assert.Equal(SubmitRejection.NotFound, Xunit.Assert.IsType<SubmitActionResult.Rejected>(result).Kind);
    }

    [Xunit.Fact]
    public async Task SystemActionType_CannotBeSubmitted()
    {
        var result = await _handler.HandleAsync(
            new SubmitActionCommand(_sessionId, SeatX, null, SystemActionTypes.PlayerRemoved, null), CancellationToken.None);

        Xunit.Assert.Equal(SubmitRejection.InvalidAction, Xunit.Assert.IsType<SubmitActionResult.Rejected>(result).Kind);
    }

    [Xunit.Fact]
    public async Task StaleVersion_IsAConflict()
    {
        await Place(SeatX, 4, 0);

        var result = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(SeatO, 0, 0));

        Xunit.Assert.Equal(SubmitRejection.Conflict, result.Kind);
        Xunit.Assert.Single(_repository.Actions);
    }

    [Xunit.Fact]
    public async Task ConcurrentSave_IsAConflict()
    {
        _repository.FailNextSave = true;

        var result = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(SeatX, 4));

        Xunit.Assert.Equal(SubmitRejection.Conflict, result.Kind);
        Xunit.Assert.Empty(_repository.Actions);
    }

    [Xunit.Fact]
    public async Task FinishedGame_WritesResults_AndRejectsFurtherMoves()
    {
        // X: 0, 1, 2 (top row)   O: 3, 4
        await Place(SeatX, 0);
        await Place(SeatO, 3);
        await Place(SeatX, 1);
        await Place(SeatO, 4);
        var last = Xunit.Assert.IsType<SubmitActionResult.Applied>(await Place(SeatX, 2));

        Xunit.Assert.True(last.Progress.IsFinished);
        Xunit.Assert.Equal(SessionStatus.Finished, _repository.Get(_sessionId).Status);
        Xunit.Assert.Contains(new SeatResultWrite(SeatX, GameOutcome.Win, 1, null), _repository.Results);
        Xunit.Assert.Contains(new SeatResultWrite(SeatO, GameOutcome.Loss, 2, null), _repository.Results);

        var after = Xunit.Assert.IsType<SubmitActionResult.Rejected>(await Place(SeatO, 8));
        Xunit.Assert.Equal(SubmitRejection.NotRunning, after.Kind);
    }

    [Xunit.Fact]
    public async Task Replay_ReproducesTheStoredState()
    {
        await Place(SeatX, 4);
        await Place(SeatO, 0);
        await Place(SeatX, 8);
        await Place(SeatO, 2);

        var session = _repository.Get(_sessionId);
        var actions = await _repository.GetActionsAsync(_sessionId, CancellationToken.None);
        var replay = new ReplayService(_modules);

        Xunit.Assert.Equal(session.StateJson, replay.Replay(session, actions));
        Xunit.Assert.Contains("\"board\":\"O---X----\"", replay.Replay(session, actions, upToSequence: 2), StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public void Replay_SkipsSystemEvents()
    {
        var session = _repository.Get(_sessionId);
        var actions = new[]
        {
            new GameActionRecord(_sessionId, SeatX, 1, "PlaceMark", "{\"cell\":4}", null, null, DateTimeOffset.UtcNow),
            new GameActionRecord(_sessionId, SeatO, 2, SystemActionTypes.Disconnected, null, null, null, DateTimeOffset.UtcNow),
            new GameActionRecord(_sessionId, SeatO, 3, "PlaceMark", "{\"cell\":0}", null, null, DateTimeOffset.UtcNow),
        };

        var state = new ReplayService(_modules).Replay(session, actions);

        Xunit.Assert.Contains("\"board\":\"O---X----\"", state, StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public void SeededRandom_IsDeterministicPerSessionAndSequence()
    {
        static int[] Draw(IRandom random) => [.. Enumerable.Range(0, 20).Select(_ => random.Next(6))];

        Xunit.Assert.Equal(Draw(SeededRandom.ForAction(7, 3)), Draw(SeededRandom.ForAction(7, 3)));
        Xunit.Assert.NotEqual(Draw(SeededRandom.ForAction(7, 3)), Draw(SeededRandom.ForAction(7, 4)));
    }

    [Xunit.Fact]
    public void RecordingRandom_RecordsAllDraws()
    {
        var random = new RecordingRandom(SeededRandom.ForAction(1, 1));
        var first = random.Next(6);
        var second = random.Next(6);

        Xunit.Assert.Equal(new[] { first, second }, random.Draws);
    }
}
