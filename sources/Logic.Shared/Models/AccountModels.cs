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

/// <summary>Result of a password login.</summary>
public enum LoginResult
{
    Succeeded,

    /// <summary>Unknown address or wrong password (not distinguished, so addresses are not revealed).</summary>
    InvalidCredentials,

    /// <summary>Temporarily locked after too many failed attempts.</summary>
    LockedOut,

    /// <summary>Password correct, but the e-mail address is not confirmed yet.</summary>
    EmailNotConfirmed,

    /// <summary>Password correct, but the account was locked by an administrator.</summary>
    AccountDisabled,

    /// <summary>Password correct; the second factor (authenticator code) is still required.</summary>
    RequiresTwoFactor,
}

/// <summary>Login result; UserId is set when the account is known.</summary>
public sealed record LoginOutcome(LoginResult Result, Guid? UserId);

/// <summary>Account data for the signed-in user.</summary>
public sealed record AccountInfo(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Roles, bool TwoFactorEnabled);

/// <summary>Data to set up an authenticator app (TOTP).</summary>
public sealed record TwoFactorSetup(bool Enabled, string SharedKey, string AuthenticatorUri);

/// <summary>A token for a link in an e-mail, with the data needed for the mail.</summary>
public sealed record AccountToken(Guid UserId, string Email, string DisplayName, string Token);

/// <summary>Outcome of resetting a password.</summary>
public abstract record ResetPasswordResult
{
    private ResetPasswordResult()
    {
    }

    public sealed record Succeeded : ResetPasswordResult;

    /// <summary>Unknown user, invalid or expired token.</summary>
    public sealed record InvalidToken : ResetPasswordResult;

    /// <summary>The new password does not meet the rules; messages in the language of the UI.</summary>
    public sealed record InvalidPassword(IReadOnlyList<string> Errors) : ResetPasswordResult;
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

/// <summary>An account as seen by the player administration.</summary>
public sealed record UserAdminRecord(
    Guid Id,
    string DisplayName,
    string Email,
    global::Shared.Models.Administration.AccountStatus Status,
    IReadOnlyList<string> Roles,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

/// <summary>One page of accounts and the total number of matches.</summary>
public sealed record UserAdminPage(IReadOnlyList<UserAdminRecord> Items, int Total);
