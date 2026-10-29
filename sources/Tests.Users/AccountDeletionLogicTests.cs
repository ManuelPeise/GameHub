using Logic.Shared.Models;
using Logic.Users;
using Microsoft.Extensions.Configuration;
using Shared.Models.Account;
using Shared.Models.Settings;

namespace Tests.Users;

public sealed class AccountDeletionLogicTests
{
    private readonly FakeAccountStore _store = new();
    private readonly FakeAuditLog _audit = new();
    private readonly AccountDeletionLogic _logic;

    public AccountDeletionLogicTests()
    {
        _logic = new AccountDeletionLogic(new AppSettings(new ConfigurationBuilder().Build()), _store, _audit);
    }

    [Xunit.Fact]
    public async Task ConfirmedWithCorrectPassword_PseudonymizesSignsOutAndAudits()
    {
        var result = await _logic.DeleteOwnAccountAsync(
            FakeAccountStore.UserId, new DeleteAccountRequest("Geheim12345", true), CancellationToken.None);

        Xunit.Assert.Equal(AccountDeletionResult.Deleted, result);
        Xunit.Assert.Equal(FakeAccountStore.UserId, Xunit.Assert.Single(_store.Pseudonymized));
        Xunit.Assert.Equal(1, _store.SignOuts);

        var audit = Xunit.Assert.Single(_audit.Records);
        Xunit.Assert.Equal(AuditActions.AccountDeleted, audit.Action);
        Xunit.Assert.Equal(AuditResult.Success, audit.Result);
        Xunit.Assert.Equal("self", audit.Details!["by"]);
    }

    [Xunit.Fact]
    public async Task AuditEntry_ContainsNoPersonalData()
    {
        await _logic.DeleteOwnAccountAsync(FakeAccountStore.UserId, new DeleteAccountRequest("Geheim12345", true), CancellationToken.None);

        var audit = Xunit.Assert.Single(_audit.Records);
        var values = audit.Details!.Values.Append(audit.Target ?? string.Empty);
        Xunit.Assert.DoesNotContain(values, value => value.Contains('@', StringComparison.Ordinal) || value.Contains("Max", StringComparison.Ordinal));
    }

    [Xunit.Fact]
    public async Task NotConfirmed_DeletesNothing()
    {
        var result = await _logic.DeleteOwnAccountAsync(
            FakeAccountStore.UserId, new DeleteAccountRequest("Geheim12345", false), CancellationToken.None);

        Xunit.Assert.Equal(AccountDeletionResult.NotConfirmed, result);
        Xunit.Assert.Empty(_store.Pseudonymized);
        Xunit.Assert.Equal(0, _store.SignOuts);
    }

    [Xunit.Theory]
    [Xunit.InlineData("")]
    [Xunit.InlineData("falsch")]
    public async Task WrongPassword_DeletesNothing_AndAuditsFailure(string password)
    {
        _store.PasswordValid = password.Length == 0;

        var result = await _logic.DeleteOwnAccountAsync(
            FakeAccountStore.UserId, new DeleteAccountRequest(password, true), CancellationToken.None);

        Xunit.Assert.Equal(AccountDeletionResult.WrongPassword, result);
        Xunit.Assert.Empty(_store.Pseudonymized);
        Xunit.Assert.Equal(AuditResult.Failure, Xunit.Assert.Single(_audit.Records).Result);
    }

    [Xunit.Fact]
    public async Task AlreadyDeleted_ReportsNotFound_WithoutSignOut()
    {
        _store.PseudonymizeResult = false;

        var result = await _logic.DeleteOwnAccountAsync(
            FakeAccountStore.UserId, new DeleteAccountRequest("Geheim12345", true), CancellationToken.None);

        Xunit.Assert.Equal(AccountDeletionResult.NotFound, result);
        Xunit.Assert.Equal(0, _store.SignOuts);
    }
}
