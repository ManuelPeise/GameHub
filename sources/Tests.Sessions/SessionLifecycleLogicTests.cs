using Logic.Core.Ai;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Logic.TicTacToe;
using Logic.TicTacToe.Ai;
using Shared.Models.Games;

namespace Tests.Sessions;

public sealed class SessionLifecycleLogicTests
{
    private readonly InMemoryGameSessionRepository _repository = new();
    private readonly InMemoryAccountStore _accounts = new();
    private readonly RecordingScheduler _scheduler = new();
    private readonly SessionLifecycleLogic _logic;
    private readonly Guid _userA = Guid.NewGuid();
    private readonly Guid _userB = Guid.NewGuid();
    private readonly Guid _userC = Guid.NewGuid();

    public SessionLifecycleLogicTests()
    {
        _accounts.Add(_userA, "Alice");
        _accounts.Add(_userB, "Bob");
        _accounts.Add(_userC, "Carla");

        _logic = new SessionLifecycleLogic(
            _repository,
            _accounts,
            new GameModuleRegistry([new TicTacToeModule()]),
            new AiStrategyRegistry([new TicTacToeAiPlayer(new TicTacToeModule(), new RandomTicTacToeStrategy())]),
            _scheduler,
            TimeProvider.System);
    }

    [Xunit.Fact]
    public async Task CreateSingleplayer_StartsImmediately_WithHumanAndAiSeat()
    {
        var outcome = await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Singleplayer, AiDifficulties.Easy, PreferredSeat: 0),
            CancellationToken.None);

        var success = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(outcome);
        Xunit.Assert.Equal(SessionStatus.Running, success.Session.Status);
        Xunit.Assert.Null(success.Session.InviteCode);
        Xunit.Assert.Equal(2, success.Session.Seats.Count);
        Xunit.Assert.Contains(success.Session.Seats, seat => seat.Kind == SeatKind.Human && seat.UserId == _userA);
        Xunit.Assert.Contains(success.Session.Seats, seat => seat.Kind == SeatKind.Ai && seat.AiDifficulty == AiDifficulties.Easy);
    }

    [Xunit.Fact]
    public async Task CreateSingleplayer_WithSameUserAndGame_ReturnsExistingSession()
    {
        var first = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Singleplayer, AiDifficulties.Easy, PreferredSeat: 0),
            CancellationToken.None));

        var second = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Singleplayer, AiDifficulties.Easy, PreferredSeat: 1),
            CancellationToken.None));

        Xunit.Assert.Equal(first.Session.Id, second.Session.Id);
    }

    [Xunit.Fact]
    public async Task CreateMultiplayer_CreatesLobbyWithInviteCode()
    {
        var outcome = await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Multiplayer, Difficulty: null, PreferredSeat: null),
            CancellationToken.None);

        var success = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(outcome);
        Xunit.Assert.Equal(GameMode.Multiplayer, success.Session.Mode);
        Xunit.Assert.Equal(SessionStatus.Lobby, success.Session.Status);
        Xunit.Assert.NotNull(success.Session.InviteCode);
        Xunit.Assert.Equal(6, success.Session.InviteCode!.Length);
    }

    [Xunit.Fact]
    public async Task Join_FailsForUnknownCode()
    {
        var outcome = await _logic.JoinAsync(_userB, new JoinSessionRequest("ABC123"), CancellationToken.None);
        var failure = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(outcome);
        Xunit.Assert.Equal(SessionLifecycleFailureKind.NotFound, failure.Kind);
    }

    [Xunit.Fact]
    public async Task Join_RejectsDuplicateAndFullLobby()
    {
        var created = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Multiplayer, Difficulty: null, PreferredSeat: null),
            CancellationToken.None));

        var joined = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.JoinAsync(
            _userB,
            new JoinSessionRequest(created.Session.InviteCode!),
            CancellationToken.None));
        Xunit.Assert.Equal(2, joined.Session.Seats.Count);

        var duplicate = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.JoinAsync(
            _userB,
            new JoinSessionRequest(created.Session.InviteCode!),
            CancellationToken.None));
        Xunit.Assert.Equal(joined.MySeatId, duplicate.MySeatId);

        var full = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(await _logic.JoinAsync(
            _userC,
            new JoinSessionRequest(created.Session.InviteCode!),
            CancellationToken.None));
        Xunit.Assert.Equal(SessionLifecycleFailureKind.Conflict, full.Kind);
    }

    [Xunit.Fact]
    public async Task Start_RequiresOwnerAndMinimumSeats()
    {
        var created = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Multiplayer, Difficulty: null, PreferredSeat: null),
            CancellationToken.None));

        var tooFew = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(await _logic.StartAsync(_userA, created.Session.Id, CancellationToken.None));
        Xunit.Assert.Equal(SessionLifecycleFailureKind.InvalidState, tooFew.Kind);

        await _logic.JoinAsync(_userB, new JoinSessionRequest(created.Session.InviteCode!), CancellationToken.None);

        var forbidden = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(
            await _logic.StartAsync(_userB, created.Session.Id, CancellationToken.None));
        Xunit.Assert.Equal(SessionLifecycleFailureKind.Forbidden, forbidden.Kind);

        var started = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(
            await _logic.StartAsync(_userA, created.Session.Id, CancellationToken.None));
        Xunit.Assert.Equal(SessionStatus.Running, started.Session.Status);
        Xunit.Assert.Null(started.Session.InviteCode);
    }

    [Xunit.Fact]
    public async Task JoinAfterStart_IsRejected()
    {
        var created = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Multiplayer, Difficulty: null, PreferredSeat: null),
            CancellationToken.None));
        await _logic.JoinAsync(_userB, new JoinSessionRequest(created.Session.InviteCode!), CancellationToken.None);
        await _logic.StartAsync(_userA, created.Session.Id, CancellationToken.None);

        var joinAfterStart = Xunit.Assert.IsType<SessionLifecycleOutcome.Failure>(await _logic.JoinAsync(
            _userC,
            new JoinSessionRequest(created.Session.InviteCode!),
            CancellationToken.None));
        Xunit.Assert.Equal(SessionLifecycleFailureKind.NotFound, joinAfterStart.Kind);
    }

    [Xunit.Fact]
    public async Task Rematch_CreatesNewSession_KeepingSeatSetup()
    {
        var created = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(await _logic.CreateAsync(
            _userA,
            new CreateSessionRequest(GameTypes.TicTacToe, GameMode.Multiplayer, Difficulty: null, PreferredSeat: null),
            CancellationToken.None));
        await _logic.JoinAsync(_userB, new JoinSessionRequest(created.Session.InviteCode!), CancellationToken.None);
        var started = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(
            await _logic.StartAsync(_userA, created.Session.Id, CancellationToken.None));

        var rematch = Xunit.Assert.IsType<SessionLifecycleOutcome.Success>(
            await _logic.RematchAsync(_userA, started.Session.Id, CancellationToken.None));

        Xunit.Assert.NotEqual(started.Session.Id, rematch.Session.Id);
        Xunit.Assert.Equal(started.Session.Seats.Count, rematch.Session.Seats.Count);
        Xunit.Assert.All(rematch.Session.Seats, seat => Xunit.Assert.Equal(SeatConnectionStatus.Connected, seat.ConnectionStatus));
    }

    private sealed class RecordingScheduler : IAiTurnScheduler
    {
        public List<Guid> Enqueued { get; } = [];

        public ValueTask EnqueueAsync(Guid sessionId, CancellationToken cancellationToken)
        {
            Enqueued.Add(sessionId);
            return ValueTask.CompletedTask;
        }

        public async IAsyncEnumerable<Guid> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var sessionId in Enqueued)
            {
                yield return sessionId;
            }
        }
    }

    private sealed class InMemoryAccountStore : IUserAccountStore
    {
        private readonly Dictionary<Guid, AccountInfo> _accounts = [];

        public void Add(Guid userId, string displayName) =>
            _accounts[userId] = new AccountInfo(userId, $"{displayName.ToLowerInvariant()}@example.org", displayName, [Shared.Models.Account.RoleNames.Player], false);

        public Task<AccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_accounts.TryGetValue(userId, out var account) ? account : null);

        public Task<CreateAccountResult> CreateAsync(string email, string displayName, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LoginOutcome> PasswordSignInAsync(string email, string password, bool persistent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginOutcome> TwoFactorSignInAsync(string code, bool persistent, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<LoginOutcome> RecoveryCodeSignInAsync(string recoveryCode, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<TwoFactorSetup?> GetTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>?> EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> PseudonymizeAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AccountToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<AccountToken?> CreateEmailConfirmationTokenAsync(string email, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
