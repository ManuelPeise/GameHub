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
