using Logic.Shared.Interfaces;
using Logic.Shared.Models;

namespace Tests.Users;

internal sealed class FakeAccountStore : IUserAccountStore
{
    public static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    public CreateAccountResult NextResult { get; set; } = new CreateAccountResult.Created(UserId, "token/with+special=chars");

    public bool ConfirmResult { get; set; } = true;

    public LoginOutcome LoginOutcome { get; set; } = new(LoginResult.Succeeded, UserId);

    public AccountInfo? Account { get; set; } = new(UserId, "max@example.org", "Max", ["Player"]);

    public AccountToken? Token { get; set; } = new(UserId, "max@example.org", "Max", "reset/token");

    public ResetPasswordResult ResetResult { get; set; } = new ResetPasswordResult.Succeeded();

    public List<(string Email, string DisplayName, string Password)> Created { get; } = [];

    public List<(string Email, bool Persistent)> SignIns { get; } = [];

    public int SignOuts { get; private set; }

    public List<string> TokenRequests { get; } = [];

    public Task<CreateAccountResult> CreateAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        Created.Add((email, displayName, password));
        return Task.FromResult(NextResult);
    }

    public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken) =>
        Task.FromResult(ConfirmResult);

    public Task<LoginOutcome> PasswordSignInAsync(string email, string password, bool persistent, CancellationToken cancellationToken)
    {
        SignIns.Add((email, persistent));
        return Task.FromResult(LoginOutcome);
    }

    public Task SignOutAsync(CancellationToken cancellationToken)
    {
        SignOuts++;
        return Task.CompletedTask;
    }

    public Task<AccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Account);

    public Task<AccountToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        TokenRequests.Add(email);
        return Task.FromResult(Token);
    }

    public Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken) =>
        Task.FromResult(ResetResult);

    public Task<AccountToken?> CreateEmailConfirmationTokenAsync(string email, CancellationToken cancellationToken)
    {
        TokenRequests.Add(email);
        return Task.FromResult(Token);
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    public bool Fail { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Fail)
        {
            throw new InvalidOperationException("SMTP not reachable");
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeAuditLog : IAuditLog
{
    public List<AuditRecord> Records { get; } = [];

    public Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        Records.Add(record);
        return Task.CompletedTask;
    }
}
