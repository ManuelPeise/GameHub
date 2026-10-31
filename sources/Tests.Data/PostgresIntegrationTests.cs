using Data.Database;
using Data.Database.Entities;
using Data.DataAccessor.Administration;
using Data.DataAccessor.Games;
using Data.DataAccessor.Statistics;
using Logic.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Models.Games;

namespace Tests.Data;

/// <summary>
/// Integration tests against a real PostgreSQL (US-030/US-032): migrations, constraints and the session
/// repository including the parts the in-memory fakes cannot check (unique indexes, concurrency token,
/// moving seats up in one transaction).
/// </summary>
public sealed class PostgresIntegrationTests(PostgresDatabase postgres) : Xunit.IClassFixture<PostgresDatabase>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private GameDbContext Context()
    {
        Xunit.Assert.SkipUnless(postgres.IsAvailable, $"{PostgresDatabase.ServerVariable} is not set.");
        return postgres.CreateContext();
    }

    private static async Task<Guid> AddUserAsync(GameDbContext database, string name)
    {
        var id = Guid.NewGuid();
        database.Users.Add(new User
        {
            Id = id,
            UserName = $"{name}-{id:N}@test.local",
            NormalizedUserName = $"{name}-{id:N}@TEST.LOCAL".ToUpperInvariant(),
            Email = $"{name}-{id:N}@test.local",
            NormalizedEmail = $"{name}-{id:N}@TEST.LOCAL".ToUpperInvariant(),
            DisplayName = name,
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = Now,
        });
        await database.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        return id;
    }

    private static SeatCreateWrite HumanSeat(int order, Guid userId, string name) =>
        new(Guid.NewGuid(), order, SeatKind.Human, name, null, userId, null, null, SeatConnectionStatus.Connected, null);

    private static SeatCreateWrite AiSeat(int order) =>
        new(Guid.NewGuid(), order, SeatKind.Ai, "KI (Easy)", null, null, "Easy", "1", SeatConnectionStatus.Connected, null);

    private static SessionCreateWrite Lobby(Guid owner, params SeatCreateWrite[] seats) =>
        new(Guid.NewGuid(), GameTypes.Kniffel, GameMode.Multiplayer, SessionStatus.Lobby, owner, null, seats[0].Id, "{}", 0, 1, Now, seats);

    [Xunit.Fact]
    public async Task Migrations_AreComplete_AndMatchTheModel()
    {
        await using var database = Context();

        Xunit.Assert.Empty(await database.Database.GetPendingMigrationsAsync(Xunit.TestContext.Current.CancellationToken));
        Xunit.Assert.False(database.Database.HasPendingModelChanges(), "The model has changes without a migration (Add-Migration missing).");
    }

    [Xunit.Fact]
    public async Task SeatCheckConstraint_RejectsHumanWithoutUser()
    {
        await using var database = Context();
        var owner = await AddUserAsync(database, "owner");
        var repository = new GameSessionRepository(database);
        var broken = new SeatCreateWrite(Guid.NewGuid(), 1, SeatKind.Human, "Niemand", null, null, null, null, SeatConnectionStatus.Connected, null);

        var lobby = Lobby(owner, HumanSeat(0, owner, "Owner"), broken);

        Xunit.Assert.False(await repository.CreateSessionAsync(lobby, CancellationToken.None));
        Xunit.Assert.Null(await repository.GetAsync(lobby.Id, CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task RemoveSeat_MovesFollowingSeatsUp_InOneTransaction()
    {
        await using var database = Context();
        var owner = await AddUserAsync(database, "owner");
        var guest = await AddUserAsync(database, "guest");
        var repository = new GameSessionRepository(database);
        var ai = AiSeat(1);
        var guestSeat = HumanSeat(2, guest, "Guest");
        var lobby = Lobby(owner, HumanSeat(0, owner, "Owner"), ai, guestSeat, AiSeat(3));
        Xunit.Assert.True(await repository.CreateSessionAsync(lobby, CancellationToken.None));

        var removed = await repository.RemoveSeatAsync(
            new SeatRemoveWrite(lobby.Id, 0, ai.Id, [new SeatReorder(guestSeat.Id, 1, null), new SeatReorder(lobby.Seats[3].Id, 2, null)], Now),
            CancellationToken.None);

        Xunit.Assert.True(removed);
        var session = await repository.GetAsync(lobby.Id, CancellationToken.None);
        Xunit.Assert.Equal(new[] { 0, 1, 2 }, session!.Seats.Select(seat => seat.Order).ToArray());
        Xunit.Assert.Equal(guestSeat.Id, session.Seats[1].Id);
        Xunit.Assert.Equal(1, session.StateVersion);

        // A stale version is rejected and changes nothing.
        Xunit.Assert.False(await repository.RemoveSeatAsync(
            new SeatRemoveWrite(lobby.Id, 0, guestSeat.Id, [], Now), CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task SaveAction_WritesStateAndLog_AndRejectsStaleVersions_AndDuplicateSequences()
    {
        await using var database = Context();
        var owner = await AddUserAsync(database, "owner");
        var repository = new GameSessionRepository(database);
        var seat = HumanSeat(0, owner, "Owner");
        var create = Lobby(owner, seat, AiSeat(1)) with { Status = SessionStatus.Running };
        Xunit.Assert.True(await repository.CreateSessionAsync(create, CancellationToken.None));

        ActionWrite Write(int expectedVersion, int sequence) => new(
            create.Id,
            expectedVersion,
            new GameActionRecord(create.Id, seat.Id, sequence, "Roll", "{\"heldMask\":0}", "[1,2,3,4,5]", null, Now),
            "{\"rollsUsed\":1}",
            SessionStatus.Running,
            seat.Id,
            [],
            TurnStartedAt: Now);

        Xunit.Assert.True(await repository.SaveActionAsync(Write(0, 1), CancellationToken.None));
        Xunit.Assert.False(await repository.SaveActionAsync(Write(0, 2), CancellationToken.None)); // stale version

        await using var other = postgres.CreateContext();
        var otherRepository = new GameSessionRepository(other);
        Xunit.Assert.False(await otherRepository.SaveActionAsync(Write(1, 1), CancellationToken.None)); // sequence 1 exists

        var actions = await repository.GetActionsAsync(create.Id, CancellationToken.None);
        var action = Xunit.Assert.Single(actions);
        Xunit.Assert.Equal("[1, 2, 3, 4, 5]", action.RandomResultJson);
        var stored = await repository.GetAsync(create.Id, CancellationToken.None);
        Xunit.Assert.Equal(1, stored!.StateVersion);
        Xunit.Assert.Equal(Now, stored.TurnStartedAt);
    }

    [Xunit.Fact]
    public async Task OnlyOneRematch_PerSession()
    {
        await using var database = Context();
        var owner = await AddUserAsync(database, "owner");
        var repository = new GameSessionRepository(database);
        var previous = Lobby(owner, HumanSeat(0, owner, "Owner"), AiSeat(1)) with { Status = SessionStatus.Finished };
        Xunit.Assert.True(await repository.CreateSessionAsync(previous, CancellationToken.None));

        SessionCreateWrite Rematch() =>
            Lobby(owner, HumanSeat(0, owner, "Owner"), AiSeat(1)) with { Status = SessionStatus.Running, PreviousSessionId = previous.Id };

        var first = Rematch();
        Xunit.Assert.True(await repository.CreateSessionAsync(first, CancellationToken.None));

        await using var other = postgres.CreateContext();
        Xunit.Assert.False(await new GameSessionRepository(other).CreateSessionAsync(Rematch(), CancellationToken.None));
        Xunit.Assert.Equal(first.Id, (await repository.FindRematchAsync(previous.Id, CancellationToken.None))!.Id);
    }

    [Xunit.Fact]
    public async Task Queries_ForTimeoutsHistoryAndInactiveSessions_Work()
    {
        await using var database = Context();
        var owner = await AddUserAsync(database, "owner");
        var guest = await AddUserAsync(database, "guest");
        var repository = new GameSessionRepository(database);
        var ownerSeat = HumanSeat(0, owner, "Owner");
        var running = Lobby(owner, ownerSeat, HumanSeat(1, guest, "Guest")) with { Status = SessionStatus.Running };
        Xunit.Assert.True(await repository.CreateSessionAsync(running, CancellationToken.None));

        var timeouts = await repository.GetTurnTimeoutsAsync(Now.AddMinutes(1), CancellationToken.None);
        Xunit.Assert.Contains(timeouts, seat => seat.SessionId == running.Id && seat.SeatId == ownerSeat.Id);
        Xunit.Assert.DoesNotContain(await repository.GetTurnTimeoutsAsync(Now.AddMinutes(-1), CancellationToken.None), seat => seat.SessionId == running.Id);

        Xunit.Assert.Contains(running.Id, await repository.GetInactiveSessionIdsAsync(Now.AddDays(1), CancellationToken.None));
        Xunit.Assert.Contains(running.Id, await repository.GetSessionIdsForUserAsync(guest, [SessionStatus.Running], CancellationToken.None));

        Xunit.Assert.True(await repository.SetStatusAsync(running.Id, 0, SessionStatus.Aborted, Now, CancellationToken.None));
        var history = await repository.GetHistoryAsync(owner, 0, 10, CancellationToken.None);
        var entry = Xunit.Assert.Single(history, record => record.SessionId == running.Id);
        Xunit.Assert.Equal(2, entry.Seats.Count);
    }

    [Xunit.Fact]
    public async Task StatisticsStore_ReturnsHumanResultsOfFinishedSessions_WithAiDifficulty()
    {
        await using var database = Context();
        var player = await AddUserAsync(database, "player");
        var repository = new GameSessionRepository(database);
        var human = HumanSeat(0, player, "Player");
        var ai = new SeatCreateWrite(Guid.NewGuid(), 1, SeatKind.Ai, "KI (Hard)", null, null, "Hard", "1", SeatConnectionStatus.Connected, null);
        var session = Lobby(player, human, ai) with { Mode = GameMode.Singleplayer, Status = SessionStatus.Running, GameType = GameTypes.TicTacToe };
        Xunit.Assert.True(await repository.CreateSessionAsync(session, CancellationToken.None));
        Xunit.Assert.True(await repository.SaveActionAsync(
            new ActionWrite(
                session.Id,
                0,
                new GameActionRecord(session.Id, human.Id, 1, "PlaceMark", "{\"cell\":4}", null, null, Now),
                "{}",
                SessionStatus.Finished,
                null,
                [new SeatResultWrite(human.Id, GameOutcome.Win, 1, null), new SeatResultWrite(ai.Id, GameOutcome.Loss, 2, null)]),
            CancellationToken.None));

        var store = new StatisticsStore(database);
        var rows = await store.GetResultsAsync(player, null, null, null, CancellationToken.None);

        var row = Xunit.Assert.Single(rows);
        Xunit.Assert.Equal(GameOutcome.Win, row.Outcome);
        Xunit.Assert.Equal("Hard", row.AiDifficulty);
        Xunit.Assert.Equal("player", row.DisplayName);
        Xunit.Assert.False(row.IsDeleted);
        Xunit.Assert.Empty(await store.GetResultsAsync(player, GameMode.Multiplayer, null, null, CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task AdminLogStore_FiltersAndPagesAppAndAuditLog()
    {
        await using var database = Context();
        var user = await AddUserAsync(database, "suchbar");
        for (var i = 0; i < 5; i++)
        {
            database.AppLog.Add(new AppLogEntry
            {
                Timestamp = Now.AddMinutes(i),
                Level = i % 2 == 0 ? "Error" : "Information",
                Category = "Tests",
                Message = $"Nachricht {i}",
            });
            database.AuditEntries.Add(new AuditEntry
            {
                CreatedAt = Now.AddMinutes(i),
                UserId = i < 3 ? user : null,
                Action = i < 3 ? "Account.Locked" : "Account.Registered",
                Result = "Success",
            });
        }

        await database.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken);
        var store = new AdminLogStore(database);

        var errors = await store.GetAppLogAsync(new AppLogFilter(null, null, "Warning", "Nachricht", null, 2), CancellationToken.None);
        Xunit.Assert.Equal(new[] { "Nachricht 4", "Nachricht 2" }, errors.Items.Select(entry => entry.Message).ToArray());
        Xunit.Assert.NotNull(errors.NextBeforeId);
        var older = await store.GetAppLogAsync(new AppLogFilter(null, null, "Warning", "Nachricht", errors.NextBeforeId, 2), CancellationToken.None);
        Xunit.Assert.Equal("Nachricht 0", Xunit.Assert.Single(older.Items).Message);
        Xunit.Assert.Null(older.NextBeforeId);

        var audit = await store.GetAuditLogAsync(new AuditFilter(null, null, "suchbar", "Locked", null, 10), CancellationToken.None);
        Xunit.Assert.Equal(3, audit.Items.Count);
        Xunit.Assert.All(audit.Items, entry => Xunit.Assert.Equal("suchbar", entry.UserName));
    }
}
