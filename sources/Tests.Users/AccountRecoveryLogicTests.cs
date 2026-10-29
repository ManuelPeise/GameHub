using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Account;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class AccountRecoveryLogicTests
{
    private const string BaseUrl = "https://localhost:5173";

    private readonly FakeAccountStore _store = new();
    private readonly FakeEmailSender _mail = new();
    private readonly FakeAuditLog _audit = new();
    private readonly AccountRecoveryLogic _logic;

    public AccountRecoveryLogicTests()
    {
        _logic = new AccountRecoveryLogic(
            new AppSettings(new ConfigurationBuilder().Build()),
            _store,
            new AccountMailer(_mail, _audit, NullLogger<AccountMailer>.Instance),
            _audit);
    }

    [Xunit.Fact]
    public async Task PasswordReset_KnownAccount_SendsLinkToResetPage()
    {
        await _logic.RequestPasswordResetAsync(" max@example.org ", BaseUrl, CancellationToken.None);

        Xunit.Assert.Equal("max@example.org", Xunit.Assert.Single(_store.TokenRequests));
        var mail = Xunit.Assert.Single(_mail.Sent);
        Xunit.Assert.Equal("max@example.org", mail.To);
        Xunit.Assert.Contains(
            $"{BaseUrl}/reset-password?userId={FakeAccountStore.UserId}&token=reset%2Ftoken",
            mail.TextBody,
            StringComparison.Ordinal);
        Xunit.Assert.Equal(AuditResult.Success, Xunit.Assert.Single(_audit.Records).Result);
    }

    [Xunit.Fact]
    public async Task PasswordReset_UnknownAccount_SendsNothing_ButAudits()
    {
        _store.Token = null;

        await _logic.RequestPasswordResetAsync("unknown@example.org", BaseUrl, CancellationToken.None);

        Xunit.Assert.Empty(_mail.Sent);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.PasswordResetRequested, audit.Action);
        Xunit.Assert.Equal(AuditResult.Failure, audit.Result);
        Xunit.Assert.Null(audit.UserId);
    }

    [Xunit.Fact]
    public async Task ResetPassword_Success_IsAudited()
    {
        var result = await _logic.ResetPasswordAsync(
            new ResetPasswordRequest(FakeAccountStore.UserId, "token", "NeuesGeheim123"),
            CancellationToken.None);

        Xunit.Assert.IsType<ResetPasswordResult.Succeeded>(result);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.PasswordReset, audit.Action);
        Xunit.Assert.Equal(AuditResult.Success, audit.Result);
    }

    [Xunit.Fact]
    public async Task ResetPassword_InvalidToken_IsAuditedAsFailure()
    {
        _store.ResetResult = new ResetPasswordResult.InvalidToken();

        var result = await _logic.ResetPasswordAsync(
            new ResetPasswordRequest(FakeAccountStore.UserId, "token", "NeuesGeheim123"),
            CancellationToken.None);

        Xunit.Assert.IsType<ResetPasswordResult.InvalidToken>(result);
        Xunit.Assert.Equal(AuditResult.Failure, Xunit.Assert.Single(_audit.Records).Result);
    }

    [Xunit.Fact]
    public async Task ResetPassword_WeakPassword_ReturnsErrorsWithoutAudit()
    {
        _store.ResetResult = new ResetPasswordResult.InvalidPassword(["zu kurz"]);

        var result = await _logic.ResetPasswordAsync(
            new ResetPasswordRequest(FakeAccountStore.UserId, "token", "kurz"),
            CancellationToken.None);

        Xunit.Assert.IsType<ResetPasswordResult.InvalidPassword>(result);
        Xunit.Assert.Empty(_audit.Records);
    }

    [Xunit.Theory]
    [Xunit.InlineData("", "NeuesGeheim123", typeof(ResetPasswordResult.InvalidToken))]
    [Xunit.InlineData("token", "", typeof(ResetPasswordResult.InvalidPassword))]
    public async Task ResetPassword_MissingInput_IsRejected(string token, string password, Type expected)
    {
        var result = await _logic.ResetPasswordAsync(
            new ResetPasswordRequest(FakeAccountStore.UserId, token, password),
            CancellationToken.None);

        Xunit.Assert.IsType(expected, result);
    }

    [Xunit.Fact]
    public async Task ResendConfirmation_UnconfirmedAccount_SendsConfirmationLink()
    {
        await _logic.ResendConfirmationAsync("max@example.org", BaseUrl, CancellationToken.None);

        var mail = Xunit.Assert.Single(_mail.Sent);
        Xunit.Assert.Contains(AccountMailer.ConfirmEmailPath, mail.TextBody, StringComparison.Ordinal);
        Xunit.Assert.Equal(AuditActions.ConfirmationEmailResent, Xunit.Assert.Single(_audit.Records).Action);
    }

    [Xunit.Fact]
    public async Task ResendConfirmation_UnknownOrConfirmed_SendsNothing()
    {
        _store.Token = null;

        await _logic.ResendConfirmationAsync("max@example.org", BaseUrl, CancellationToken.None);

        Xunit.Assert.Empty(_mail.Sent);
        Xunit.Assert.Equal(AuditResult.Failure, Xunit.Assert.Single(_audit.Records).Result);
    }
}
