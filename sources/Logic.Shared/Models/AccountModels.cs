namespace Logic.Shared.Models;

/// <summary>Outcome of creating a user account in the account store.</summary>
public abstract record CreateAccountResult
{
    private CreateAccountResult()
    {
    }

    /// <summary>Account created (role Player, e-mail not yet confirmed) with a token for the confirmation link.</summary>
    public sealed record Created(Guid UserId, string EmailConfirmationToken) : CreateAccountResult;

    /// <summary>An account with this e-mail address already exists.</summary>
    public sealed record DuplicateEmail : CreateAccountResult;

    /// <summary>The password does not meet the rules; messages are in the language of the UI.</summary>
    public sealed record InvalidPassword(IReadOnlyList<string> Errors) : CreateAccountResult;
}

/// <summary>An e-mail to send.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);

/// <summary>Result of an audited action.</summary>
public enum AuditResult
{
    Success,
    Failure,
}

/// <summary>
/// An entry for the audit log. Must not contain personal data such as e-mail addresses or names:
/// it is kept when a user is deleted.
/// </summary>
public sealed record AuditRecord(
    string Action,
    AuditResult Result,
    Guid? UserId = null,
    string? Target = null,
    IReadOnlyDictionary<string, string>? Details = null);
