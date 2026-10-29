using Logic.Shared.Interfaces;
using Logic.Shared.Models;

namespace Tests.Users;

internal sealed class FakeAccountStore : IUserAccountStore
{
    public CreateAccountResult NextResult { get; set; } =
        new CreateAccountResult.Created(Guid.Parse("11111111-2222-3333-4444-555555555555"), "token/with+special=chars");

    public bool ConfirmResult { get; set; } = true;

    public List<(string Email, string DisplayName, string Password)> Created { get; } = [];

    public Task<CreateAccountResult> CreateAsync(string email, string displayName, string password, CancellationToken cancellationToken)
    {
        Created.Add((email, displayName, password));
        return Task.FromResult(NextResult);
    }

    public Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken) =>
        Task.FromResult(ConfirmResult);
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
