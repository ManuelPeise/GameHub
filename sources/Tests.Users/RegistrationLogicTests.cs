using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Account;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class RegistrationLogicTests
{
    private const string BaseUrl = "https://localhost:5173/";

    private readonly FakeAccountStore _store = new();
    private readonly FakeEmailSender _mail = new();
    private readonly FakeAuditLog _audit = new();
    private readonly RegistrationLogic _logic;

    public RegistrationLogicTests()
    {
        _logic = new RegistrationLogic(
            new AppSettings(new ConfigurationBuilder().Build()),
            _store,
            new AccountMailer(_mail, _audit, NullLogger<AccountMailer>.Instance),
            _audit);
    }

    private static RegisterRequest ValidRequest() => new("max@example.org", "Max", "Geheim12345");

    [Xunit.Fact]
    public async Task NewAddress_CreatesAccountSendsConfirmationLinkAndAudits()
    {
        var outcome = await _logic.RegisterAsync(ValidRequest(), BaseUrl, CancellationToken.None);

        Xunit.Assert.IsType<RegistrationOutcome.Accepted>(outcome);
        Xunit.Assert.Single(_store.Created);

        var mail = Xunit.Assert.Single(_mail.Sent);
        Xunit.Assert.Equal("max@example.org", mail.To);
        var expectedLink = "https://localhost:5173/api/account/confirm-email"
            + "?userId=11111111-2222-3333-4444-555555555555&token=token%2Fwith%2Bspecial%3Dchars";
        Xunit.Assert.Contains(expectedLink, mail.TextBody, StringComparison.Ordinal);

        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.Registered, audit.Action);
        Xunit.Assert.Equal(AuditResult.Success, audit.Result);
        Xunit.Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), audit.UserId);
    }

    [Xunit.Fact]
    public async Task ExistingAddress_AnswersLikeNewAddress_AndOnlyNotifiesTheOwner()
    {
        _store.NextResult = new CreateAccountResult.DuplicateEmail();

        var outcome = await _logic.RegisterAsync(ValidRequest(), BaseUrl, CancellationToken.None);

        Xunit.Assert.IsType<RegistrationOutcome.Accepted>(outcome);

        var mail = Xunit.Assert.Single(_mail.Sent);
        Xunit.Assert.Equal("max@example.org", mail.To);
        Xunit.Assert.DoesNotContain(AccountMailer.ConfirmEmailPath, mail.TextBody, StringComparison.Ordinal);

        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.RegistrationWithExistingEmail, audit.Action);
        Xunit.Assert.Null(audit.UserId);
        Xunit.Assert.Null(audit.Target);
    }

    [Xunit.Fact]
    public async Task InvalidInput_ReturnsErrorsPerField_AndCreatesNothing()
    {
        var outcome = await _logic.RegisterAsync(new RegisterRequest("no-address", "Mx", ""), BaseUrl, CancellationToken.None);

        var invalid = Xunit.Assert.IsType<RegistrationOutcome.Invalid>(outcome);
        Xunit.Assert.Contains("email", invalid.Errors.Keys);
        Xunit.Assert.Contains("displayName", invalid.Errors.Keys);
        Xunit.Assert.Contains("password", invalid.Errors.Keys);
        Xunit.Assert.Empty(_store.Created);
        Xunit.Assert.Empty(_mail.Sent);
        Xunit.Assert.Empty(_audit.Records);
    }

    [Xunit.Theory]
    [Xunit.InlineData("Max")]
    [Xunit.InlineData("  Max Mustermann  ")]
    [Xunit.InlineData("abcdefghijklmnopqrstuvwxyzABCDEF")]
    public async Task DisplayName_WithinLimits_IsAcceptedAndTrimmed(string displayName)
    {
        await _logic.RegisterAsync(ValidRequest() with { DisplayName = displayName }, BaseUrl, CancellationToken.None);

        Xunit.Assert.Equal(displayName.Trim(), Xunit.Assert.Single(_store.Created).DisplayName);
    }

    [Xunit.Fact]
    public async Task DisplayName_TooLong_IsRejected()
    {
        var outcome = await _logic.RegisterAsync(
            ValidRequest() with { DisplayName = new string('x', AccountRules.DisplayNameMaxLength + 1) },
            BaseUrl,
            CancellationToken.None);

        Xunit.Assert.Contains("displayName", Xunit.Assert.IsType<RegistrationOutcome.Invalid>(outcome).Errors.Keys);
    }

    [Xunit.Fact]
    public async Task PasswordRejectedByStore_ReturnsPasswordErrors()
    {
        _store.NextResult = new CreateAccountResult.InvalidPassword(["Das Passwort muss mindestens eine Ziffer enthalten."]);

        var outcome = await _logic.RegisterAsync(ValidRequest(), BaseUrl, CancellationToken.None);

        var invalid = Xunit.Assert.IsType<RegistrationOutcome.Invalid>(outcome);
        Xunit.Assert.Equal(new[] { "Das Passwort muss mindestens eine Ziffer enthalten." }, invalid.Errors["password"]);
        Xunit.Assert.Empty(_mail.Sent);
    }

    [Xunit.Fact]
    public async Task MailFailure_StillAccepted_AndAudited()
    {
        _mail.Fail = true;

        var outcome = await _logic.RegisterAsync(ValidRequest(), BaseUrl, CancellationToken.None);

        Xunit.Assert.IsType<RegistrationOutcome.Accepted>(outcome);
        Xunit.Assert.Contains(_audit.Records, record => record.Action == AuditActions.EmailFailed);
        Xunit.Assert.Contains(_audit.Records, record => record.Action == AuditActions.Registered);
    }

    [Xunit.Theory]
    [Xunit.InlineData(true, AuditResult.Success)]
    [Xunit.InlineData(false, AuditResult.Failure)]
    public async Task ConfirmEmail_ReturnsStoreResult_AndAudits(bool storeResult, AuditResult expectedAudit)
    {
        _store.ConfirmResult = storeResult;
        var userId = Guid.NewGuid();

        var confirmed = await _logic.ConfirmEmailAsync(userId, "token", CancellationToken.None);

        Xunit.Assert.Equal(storeResult, confirmed);
        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.EmailConfirmed, audit.Action);
        Xunit.Assert.Equal(expectedAudit, audit.Result);
        Xunit.Assert.Equal(userId, audit.UserId);
    }

    [Xunit.Fact]
    public async Task ConfirmEmail_WithoutToken_IsRejectedWithoutStoreCall()
    {
        var confirmed = await _logic.ConfirmEmailAsync(Guid.NewGuid(), " ", CancellationToken.None);

        Xunit.Assert.False(confirmed);
    }
}
