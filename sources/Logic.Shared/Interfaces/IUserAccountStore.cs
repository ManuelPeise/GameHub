using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for user accounts and sign-in (implemented in Data.DataAccessor with ASP.NET Core Identity).</summary>
public interface IUserAccountStore
{
    /// <summary>Creates an account with role Player; it stays inactive until the e-mail is confirmed.</summary>
    Task<CreateAccountResult> CreateAsync(string email, string displayName, string password, CancellationToken cancellationToken);

    /// <summary>Confirms the e-mail address; false if user or token are invalid or expired.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);

    /// <summary>
    /// Checks the password, counts failed attempts (temporary lock) and on success signs the user in
    /// (authentication cookie) and records the login time.
    /// </summary>
    Task<LoginOutcome> PasswordSignInAsync(string email, string password, bool persistent, CancellationToken cancellationToken);

    /// <summary>Second login step with a code of the authenticator app (after <see cref="LoginResult.RequiresTwoFactor"/>).</summary>
    Task<LoginOutcome> TwoFactorSignInAsync(string code, bool persistent, CancellationToken cancellationToken);

    /// <summary>Second login step with one of the recovery codes (each code works once).</summary>
    Task<LoginOutcome> RecoveryCodeSignInAsync(string recoveryCode, CancellationToken cancellationToken);

    /// <summary>Key and otpauth URI for the authenticator app; creates a key if the user has none.</summary>
    Task<TwoFactorSetup?> GetTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Enables two-factor authentication if the code is valid, renews the sign-in as two-factor
    /// sign-in and returns new recovery codes; null if the code is invalid.
    /// </summary>
    Task<IReadOnlyList<string>?> EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken);

    /// <summary>Removes the authentication cookie.</summary>
    Task SignOutAsync(CancellationToken cancellationToken);

    /// <summary>Active account by id, or null.</summary>
    Task<AccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Token for a password reset link; null if there is no active account with this address.</summary>
    Task<AccountToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken);

    /// <summary>Sets a new password with a token from <see cref="CreatePasswordResetTokenAsync"/>.</summary>
    Task<ResetPasswordResult> ResetPasswordAsync(Guid userId, string token, string newPassword, CancellationToken cancellationToken);

    /// <summary>New confirmation token; null if the address is unknown, already confirmed or the account is not active.</summary>
    Task<AccountToken?> CreateEmailConfirmationTokenAsync(string email, CancellationToken cancellationToken);
}
