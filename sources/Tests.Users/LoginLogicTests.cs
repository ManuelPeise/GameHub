using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Shared.Models.Account;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class LoginLogicTests
{
    private readonly FakeAccountStore _store = new();
    private readonly FakeAuditLog _audit = new();
    private readonly LoginLogic _logic;

    public LoginLogicTests()
    {
        _logic = new LoginLogic(new AppSettings(new ConfigurationBuilder().Build()), _store, _audit);
    }

    [Xunit.Fact]
    public async Task Success_SignsInWithRememberMe_AndAudits()
    {
        var outcome = await _logic.LoginAsync(new LoginRequest(" max@example.org ", "Geheim12345", true), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.Succeeded, outcome.Result);
        Xunit.Assert.Equal(("max@example.org", true), Xunit.Assert.Single(_store.SignIns));

        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.LoginSucceeded, audit.Action);
        Xunit.Assert.Equal(AuditResult.Success, audit.Result);
        Xunit.Assert.Equal(FakeAccountStore.UserId, audit.UserId);
        Xunit.Assert.Equal("password", audit.Details!["method"]);
    }

    [Xunit.Theory]
    [Xunit.InlineData(LoginResult.InvalidCredentials)]
    [Xunit.InlineData(LoginResult.LockedOut)]
    [Xunit.InlineData(LoginResult.EmailNotConfirmed)]
    [Xunit.InlineData(LoginResult.AccountDisabled)]
    public async Task Failure_IsReturned_AndAuditedWithReason(LoginResult result)
    {
        _store.LoginOutcome = new LoginOutcome(result, FakeAccountStore.UserId);

        var outcome = await _logic.LoginAsync(new LoginRequest("max@example.org", "falsch", false), CancellationToken.None);

        Xunit.Assert.Equal(result, outcome.Result);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.LoginFailed, audit.Action);
        Xunit.Assert.Equal(AuditResult.Failure, audit.Result);
        Xunit.Assert.Equal(result.ToString(), audit.Details!["reason"]);
    }

    [Xunit.Theory]
    [Xunit.InlineData("", "Geheim12345")]
    [Xunit.InlineData("max@example.org", "")]
    public async Task MissingInput_IsInvalidCredentials_WithoutStoreCall(string email, string password)
    {
        var outcome = await _logic.LoginAsync(new LoginRequest(email, password, false), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.InvalidCredentials, outcome.Result);
        Xunit.Assert.Empty(_store.SignIns);
    }

    [Xunit.Fact]
    public async Task Logout_SignsOut_AndAuditsKnownUser()
    {
        await _logic.LogoutAsync(FakeAccountStore.UserId, CancellationToken.None);

        Xunit.Assert.Equal(1, _store.SignOuts);
        Xunit.Assert.Equal(AuditActions.Logout, Xunit.Assert.Single(_audit.Records).Action);
    }

    [Xunit.Fact]
    public async Task Logout_WithoutUser_SignsOutWithoutAudit()
    {
        await _logic.LogoutAsync(null, CancellationToken.None);

        Xunit.Assert.Equal(1, _store.SignOuts);
        Xunit.Assert.Empty(_audit.Records);
    }

    [Xunit.Fact]
    public async Task PasswordStepOfTwoFactorLogin_IsNotAuditedYet()
    {
        _store.LoginOutcome = new LoginOutcome(LoginResult.RequiresTwoFactor, FakeAccountStore.UserId);

        var outcome = await _logic.LoginAsync(new LoginRequest("max@example.org", "Geheim12345", false), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.RequiresTwoFactor, outcome.Result);
        Xunit.Assert.Empty(_audit.Records);
    }

    [Xunit.Fact]
    public async Task TwoFactorStep_Success_IsAuditedWithMethod()
    {
        var outcome = await _logic.LoginWithTwoFactorAsync(new TwoFactorLoginRequest("123456", false), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.Succeeded, outcome.Result);
        Xunit.Assert.Equal("123456", Xunit.Assert.Single(_store.SecondFactorCodes));
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.LoginSucceeded, audit.Action);
        Xunit.Assert.Equal("authenticator", audit.Details!["method"]);
    }

    [Xunit.Fact]
    public async Task RecoveryCodeStep_Failure_IsAudited()
    {
        _store.SecondFactorOutcome = new LoginOutcome(LoginResult.InvalidCredentials, FakeAccountStore.UserId);

        var outcome = await _logic.LoginWithRecoveryCodeAsync(new RecoveryCodeLoginRequest("abcd-efgh"), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.InvalidCredentials, outcome.Result);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.LoginFailed, audit.Action);
        Xunit.Assert.Equal("recoveryCode", audit.Details!["method"]);
    }

    [Xunit.Fact]
    public async Task TwoFactorStep_WithoutCode_IsRejectedWithoutStoreCall()
    {
        var outcome = await _logic.LoginWithTwoFactorAsync(new TwoFactorLoginRequest(" ", false), CancellationToken.None);

        Xunit.Assert.Equal(LoginResult.InvalidCredentials, outcome.Result);
        Xunit.Assert.Empty(_store.SecondFactorCodes);
    }
}
