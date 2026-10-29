using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class TwoFactorLogicTests
{
    private readonly FakeAccountStore _store = new();
    private readonly FakeAuditLog _audit = new();
    private readonly TwoFactorLogic _logic;

    public TwoFactorLogicTests()
    {
        _logic = new TwoFactorLogic(new AppSettings(new ConfigurationBuilder().Build()), _store, _audit);
    }

    [Xunit.Fact]
    public async Task Enable_ValidCode_ReturnsRecoveryCodes_AndAudits()
    {
        var codes = await _logic.EnableAsync(FakeAccountStore.UserId, "123456", CancellationToken.None);

        Xunit.Assert.Equal(new[] { "code-1", "code-2" }, codes);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.TwoFactorEnabled, audit.Action);
        Xunit.Assert.Equal(AuditResult.Success, audit.Result);
    }

    [Xunit.Fact]
    public async Task Enable_InvalidCode_ReturnsNull_AndAuditsFailure()
    {
        _store.RecoveryCodes = null;

        var codes = await _logic.EnableAsync(FakeAccountStore.UserId, "000000", CancellationToken.None);

        Xunit.Assert.Null(codes);
        Xunit.Assert.Equal(AuditResult.Failure, Xunit.Assert.Single(_audit.Records).Result);
    }

    [Xunit.Fact]
    public async Task Enable_WithoutCode_IsRejectedWithoutAudit()
    {
        var codes = await _logic.EnableAsync(FakeAccountStore.UserId, "", CancellationToken.None);

        Xunit.Assert.Null(codes);
        Xunit.Assert.Empty(_audit.Records);
    }
}
