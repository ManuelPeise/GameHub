using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for user accounts (implemented in Data.DataAccessor with ASP.NET Core Identity).</summary>
public interface IUserAccountStore
{
    /// <summary>Creates an account with role Player; it stays inactive until the e-mail is confirmed.</summary>
    Task<CreateAccountResult> CreateAsync(string email, string displayName, string password, CancellationToken cancellationToken);

    /// <summary>Confirms the e-mail address; false if user or token are invalid or expired.</summary>
    Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken);
}
