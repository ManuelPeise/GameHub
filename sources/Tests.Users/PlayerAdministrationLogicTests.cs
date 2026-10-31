using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Shared.Models.Account;
using Shared.Models.Administration;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class PlayerAdministrationLogicTests
{
    private static readonly Guid AdminId = Guid.NewGuid();
    private static readonly Guid PlayerId = FakeAccountStore.UserId;

    private readonly FakeAdminStore _admin = new();
    private readonly FakeAccountStore _accounts = new();
    private readonly FakeAuditLog _audit = new();
    private readonly PlayerAdministrationLogic _logic;

    public PlayerAdministrationLogicTests()
    {
        var settings = new AppSettings(new ConfigurationBuilder().Build());
        _logic = new PlayerAdministrationLogic(settings, _admin, new AccountDeletionLogic(settings, _accounts, _audit), _audit);
        _admin.Add(AdminId, "Admin", AccountStatus.Active, RoleNames.Player, RoleNames.Administrator);
        _admin.Add(PlayerId, "Max", AccountStatus.Active, RoleNames.Player);
    }

    [Xunit.Fact]
    public async Task Search_MarksTheOwnAccount_AndLimitsThePageSize()
    {
        var page = await _logic.SearchAsync(AdminId, null, false, 0, 500, CancellationToken.None);

        Xunit.Assert.Equal(2, page.Total);
        Xunit.Assert.True(page.Items.Single(user => user.Id == AdminId).IsMe);
        Xunit.Assert.Equal(PlayerAdministrationLogic.MaxPageSize, _admin.LastTake);
    }

    [Xunit.Fact]
    public async Task LockAndUnlock_ChangeTheStatus_AndAreAudited()
    {
        Xunit.Assert.Equal(AdminActionResult.Done, await _logic.LockAsync(AdminId, PlayerId, CancellationToken.None));
        Xunit.Assert.Equal(AccountStatus.Locked, _admin.Users[PlayerId].Status);

        Xunit.Assert.Equal(AdminActionResult.InvalidState, await _logic.LockAsync(AdminId, PlayerId, CancellationToken.None));

        Xunit.Assert.Equal(AdminActionResult.Done, await _logic.UnlockAsync(AdminId, PlayerId, CancellationToken.None));
        Xunit.Assert.Equal(AccountStatus.Active, _admin.Users[PlayerId].Status);

        Xunit.Assert.Equal(
            new[] { AuditActions.AccountLocked, AuditActions.AccountLocked, AuditActions.AccountUnlocked },
            _audit.Records.Select(record => record.Action).ToArray());
        var first = _audit.Records[0];
        Xunit.Assert.Equal(AuditResult.Success, first.Result);
        Xunit.Assert.Equal(AdminId, first.UserId);
        Xunit.Assert.Equal(PlayerId.ToString(), first.Target);
        Xunit.Assert.Equal(AuditResult.Failure, _audit.Records[1].Result);
    }

    [Xunit.Fact]
    public async Task Admin_CannotLockOrDeleteThemselves()
    {
        Xunit.Assert.Equal(AdminActionResult.Self, await _logic.LockAsync(AdminId, AdminId, CancellationToken.None));
        Xunit.Assert.Equal(AdminActionResult.Self, await _logic.DeleteAsync(AdminId, AdminId, true, CancellationToken.None));
        Xunit.Assert.Empty(_accounts.Pseudonymized);
    }

    [Xunit.Fact]
    public async Task LastActiveAdministrator_CannotBeLockedOrDeleted()
    {
        var otherAdmin = Guid.NewGuid();
        _admin.Add(otherAdmin, "Zweiter Admin", AccountStatus.Active, RoleNames.Administrator);

        // Two active administrators: one may be locked …
        Xunit.Assert.Equal(AdminActionResult.Done, await _logic.LockAsync(AdminId, otherAdmin, CancellationToken.None));

        // … then the remaining one is the last; another admin account (not active) cannot remove it.
        _admin.Users[AdminId] = _admin.Users[AdminId] with { Status = AccountStatus.Active };
        var thirdAdmin = Guid.NewGuid();
        _admin.Add(thirdAdmin, "Dritter Admin", AccountStatus.Active, RoleNames.Administrator);
        _admin.Users[AdminId] = _admin.Users[AdminId] with { Status = AccountStatus.Locked };
        Xunit.Assert.Equal(AdminActionResult.LastAdministrator, await _logic.DeleteAsync(otherAdmin, thirdAdmin, true, CancellationToken.None));
    }

    [Xunit.Fact]
    public async Task Delete_NeedsConfirmation_AndUsesThePseudonymization()
    {
        Xunit.Assert.Equal(AdminActionResult.NotConfirmed, await _logic.DeleteAsync(AdminId, PlayerId, false, CancellationToken.None));
        Xunit.Assert.Empty(_accounts.Pseudonymized);

        Xunit.Assert.Equal(AdminActionResult.Done, await _logic.DeleteAsync(AdminId, PlayerId, true, CancellationToken.None));

        Xunit.Assert.Equal(PlayerId, Xunit.Assert.Single(_accounts.Pseudonymized));
        var audit = _audit.Records.Single(record => record.Action == AuditActions.AccountDeleted);
        Xunit.Assert.Equal("admin", audit.Details!["by"]);
    }

    [Xunit.Fact]
    public async Task UnknownOrDeletedPlayer_IsNotFound()
    {
        _admin.Users[PlayerId] = _admin.Users[PlayerId] with { Status = AccountStatus.Deleted };

        Xunit.Assert.Equal(AdminActionResult.NotFound, await _logic.LockAsync(AdminId, PlayerId, CancellationToken.None));
        Xunit.Assert.Equal(AdminActionResult.NotFound, await _logic.DeleteAsync(AdminId, Guid.NewGuid(), true, CancellationToken.None));
    }

    private sealed class FakeAdminStore : IUserAdministrationStore
    {
        public Dictionary<Guid, UserAdminRecord> Users { get; } = [];

        public int LastTake { get; private set; }

        public void Add(Guid id, string name, AccountStatus status, params string[] roles) =>
            Users[id] = new UserAdminRecord(id, name, $"{name}@example.org", status, roles, true, false, DateTimeOffset.UnixEpoch, null);

        public Task<UserAdminPage> SearchAsync(string? query, bool includeDeleted, int skip, int take, CancellationToken cancellationToken)
        {
            LastTake = take;
            var matches = Users.Values.Where(user => includeDeleted || user.Status != AccountStatus.Deleted).ToList();
            return Task.FromResult(new UserAdminPage([.. matches.Skip(skip).Take(take)], matches.Count));
        }

        public Task<UserAdminRecord?> FindAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Users.GetValueOrDefault(userId));

        public Task<bool> SetLockedAsync(Guid userId, bool locked, CancellationToken cancellationToken)
        {
            Users[userId] = Users[userId] with { Status = locked ? AccountStatus.Locked : AccountStatus.Active };
            return Task.FromResult(true);
        }

        public Task<int> CountActiveAdministratorsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Users.Values.Count(user => user.Status == AccountStatus.Active && user.Roles.Contains(RoleNames.Administrator)));
    }
}
