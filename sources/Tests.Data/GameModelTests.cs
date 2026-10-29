using Data.Database;
using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Tests.Data;

/// <summary>
/// Checks the game data model (US-009) on the EF Core model, without a database: constraints, indexes,
/// JSON columns, concurrency token and the append-only rule of the move and audit log.
/// </summary>
public sealed class GameModelTests : IDisposable
{
    private readonly GameDbContext _database = new GameDbContextDesignTimeFactory().CreateDbContext([]);

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// Full model including design-time information such as check constraints (the runtime model of
    /// DbContext.Model is read-optimized and does not keep them).
    /// </summary>
    private IModel DesignModel => _database.GetService<IDesignTimeModel>().Model;

    private IEntityType Entity<T>() => DesignModel.FindEntityType(typeof(T))
        ?? throw new InvalidOperationException($"{typeof(T).Name} is not part of the model.");

    [Xunit.Theory]
    [Xunit.InlineData(typeof(GameSession), nameof(GameSession.StateJson))]
    [Xunit.InlineData(typeof(GameAction), nameof(GameAction.PayloadJson))]
    [Xunit.InlineData(typeof(GameAction), nameof(GameAction.RandomResultJson))]
    public void JsonColumns_AreJsonb(Type entityType, string property)
    {
        var entity = DesignModel.FindEntityType(entityType)!;

        Xunit.Assert.Equal("jsonb", entity.FindProperty(property)!.GetColumnType());
    }

    [Xunit.Fact]
    public void Seat_HasCheckConstraint_ForHumanUserAndAiDifficulty()
    {
        var constraint = Xunit.Assert.Single(
            Entity<Seat>().GetCheckConstraints(),
            check => check.ModelName == "CK_Seat_HumanHasUser_AiHasDifficulty");

        Xunit.Assert.Contains("\"Kind\" = 'Human' AND \"UserId\" IS NOT NULL", constraint.Sql, StringComparison.Ordinal);
        Xunit.Assert.Contains("\"Kind\" = 'Ai' AND \"UserId\" IS NULL AND \"AiDifficulty\" IS NOT NULL AND \"AiStrategyVersion\" IS NOT NULL",
            constraint.Sql, StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public void Seat_UserIsOptional_SoAiOpponentsNeedNoAccount()
    {
        Xunit.Assert.True(Entity<Seat>().FindProperty(nameof(Seat.UserId))!.IsNullable);
    }

    [Xunit.Fact]
    public void GameSession_AllowsOnlyOneOpenSingleplayerSessionPerOwnerAndGame()
    {
        var index = Xunit.Assert.Single(
            Entity<GameSession>().GetIndexes(),
            candidate => candidate.GetDatabaseName() == "IX_GameSession_OneOpenSingleplayerPerOwnerAndGame");

        Xunit.Assert.True(index.IsUnique);
        Xunit.Assert.Equal(
            new[] { nameof(GameSession.OwnerUserId), nameof(GameSession.GameType) },
            index.Properties.Select(property => property.Name));
        Xunit.Assert.Equal(
            "\"Mode\" = 'Singleplayer' AND \"Status\" IN ('Running', 'Paused')",
            index.GetFilter());
    }

    [Xunit.Fact]
    public void GameSession_InviteCode_IsUniqueWhenPresent()
    {
        var index = Xunit.Assert.Single(
            Entity<GameSession>().GetIndexes(),
            candidate => candidate.GetDatabaseName() == "IX_GameSession_InviteCode");

        Xunit.Assert.True(index.IsUnique);
        Xunit.Assert.Equal([nameof(GameSession.InviteCode)], index.Properties.Select(property => property.Name));
        Xunit.Assert.Equal("\"InviteCode\" IS NOT NULL", index.GetFilter());
    }

    [Xunit.Fact]
    public void GameSession_StateVersion_IsConcurrencyToken()
    {
        Xunit.Assert.True(Entity<GameSession>().FindProperty(nameof(GameSession.StateVersion))!.IsConcurrencyToken);
    }

    [Xunit.Fact]
    public void GameAction_SequenceIsUniquePerSession()
    {
        var index = Xunit.Assert.Single(
            Entity<GameAction>().GetIndexes(),
            candidate => candidate.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(GameAction.SessionId), nameof(GameAction.Sequence)]));

        Xunit.Assert.True(index.IsUnique);
    }

    [Xunit.Theory]
    [Xunit.InlineData(typeof(GameSession), "GameSession")]
    [Xunit.InlineData(typeof(Seat), "Seat")]
    [Xunit.InlineData(typeof(GameAction), "GameAction")]
    [Xunit.InlineData(typeof(GameResult), "GameResult")]
    public void GameTables_AreInSchemaGame(Type entityType, string table)
    {
        var entity = DesignModel.FindEntityType(entityType)!;

        Xunit.Assert.Equal(GameDbContext.GameSchema, entity.GetSchema());
        Xunit.Assert.Equal(table, entity.GetTableName());
    }

    [Xunit.Fact]
    public void ChangingAGameAction_IsRejected()
    {
        var action = new GameAction { Id = 1, SessionId = Guid.NewGuid(), Sequence = 1, ActionType = "PlaceMark" };
        _database.GameActions.Attach(action);
        action.ActionType = "Changed";

        var exception = Xunit.Assert.Throws<InvalidOperationException>(() => _database.SaveChanges());
        Xunit.Assert.Contains("append-only", exception.Message, StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public async Task DeletingAGameAction_IsRejected()
    {
        var action = new GameAction { Id = 1, SessionId = Guid.NewGuid(), Sequence = 1, ActionType = "PlaceMark" };
        _database.GameActions.Attach(action);
        _database.GameActions.Remove(action);

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(
            () => _database.SaveChangesAsync(Xunit.TestContext.Current.CancellationToken));
    }

    [Xunit.Fact]
    public void ChangingAnAuditEntry_IsRejected()
    {
        var entry = new AuditEntry { Id = 1, Action = "Account.Registered", Result = "Success" };
        _database.AuditEntries.Attach(entry);
        entry.Result = "Failure";

        Xunit.Assert.Throws<InvalidOperationException>(() => _database.SaveChanges());
    }
}
