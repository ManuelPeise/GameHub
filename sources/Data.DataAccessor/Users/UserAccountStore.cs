using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Shared.Models.Account;

namespace Data.DataAccessor.Users;

/// <summary><see cref="IUserAccountStore"/> on top of ASP.NET Core Identity.</summary>
internal sealed class UserAccountStore(
    UserManager<User> userManager,
    SignInManager<User> signInManager,
    TimeProvider timeProvider) : IUserAccountStore
{
    public async Task<CreateAccountResult> CreateAsync(
        string email,
        string displayName,
        string password,
        CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return new CreateAccountResult.DuplicateEmail();
        }

        var user = new User
        {
            UserName = email,
            Email = email,
            DisplayName = displayName,
            CreatedAt = timeProvider.GetUtcNow(),
        };

        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            // Race with a parallel registration of the same address.
            if (created.Errors.Any(error => error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return new CreateAccountResult.DuplicateEmail();
            }

            return new CreateAccountResult.InvalidPassword([.. created.Errors.Select(Translate)]);
        }

        var roleAdded = await userManager.AddToRoleAsync(user, RoleNames.Player);
        if (!roleAdded.Succeeded)
        {
            throw new InvalidOperationException(
                $"Role {RoleNames.Player} could not be assigned: {string.Join("; ", roleAdded.Errors.Select(error => error.Code))}");
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        return new CreateAccountResult.Created(user.Id, token);
    }

    public async Task<bool> ConfirmEmailAsync(Guid userId, string token, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return false;
        }

        if (user.EmailConfirmed)
        {
            return true;
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded;
    }

    public async Task<LoginOutcome> PasswordSignInAsync(
        string email,
        string password,
        bool persistent,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.Status == UserStatus.Deleted)
        {
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        if (user.Status == UserStatus.Locked)
        {
            // Only tell the owner (correct password) that the account is locked.
            return await userManager.CheckPasswordAsync(user, password)
                ? new LoginOutcome(LoginResult.AccountDisabled, user.Id)
                : new LoginOutcome(LoginResult.InvalidCredentials, user.Id);
        }

        if (!user.EmailConfirmed)
        {
            // Identity would answer "not allowed" before checking the password, which reveals unconfirmed
            // accounts to anyone. Only tell the owner (correct password).
            return await userManager.CheckPasswordAsync(user, password)
                ? new LoginOutcome(LoginResult.EmailNotConfirmed, user.Id)
                : new LoginOutcome(LoginResult.InvalidCredentials, user.Id);
        }

        var result = await signInManager.PasswordSignInAsync(user, password, persistent, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = timeProvider.GetUtcNow();
            await userManager.UpdateAsync(user);
            return new LoginOutcome(LoginResult.Succeeded, user.Id);
        }

        if (result.IsLockedOut)
        {
            return new LoginOutcome(LoginResult.LockedOut, user.Id);
        }

        // IsNotAllowed: password correct, but sign-in requirements (confirmed e-mail) are not met.
        if (result.IsNotAllowed)
        {
            return new LoginOutcome(LoginResult.EmailNotConfirmed, user.Id);
        }

        return new LoginOutcome(LoginResult.InvalidCredentials, user.Id);
    }

    public Task SignOutAsync(CancellationToken cancellationToken) => signInManager.SignOutAsync();

    public async Task<AccountInfo?> FindAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return new AccountInfo(user.Id, user.Email ?? string.Empty, user.DisplayName, [.. roles]);
    }

    public async Task<AccountToken?> CreatePasswordResetTokenAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.Status != UserStatus.Active)
        {
            return null;
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        return new AccountToken(user.Id, user.Email ?? email, user.DisplayName, token);
    }

    public async Task<ResetPasswordResult> ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return new ResetPasswordResult.InvalidToken();
        }

        var result = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (result.Succeeded)
        {
            // A reset also ends a temporary lock from failed attempts.
            await userManager.SetLockoutEndDateAsync(user, null);
            await userManager.ResetAccessFailedCountAsync(user);
            return new ResetPasswordResult.Succeeded();
        }

        if (result.Errors.Any(error => error.Code == "InvalidToken"))
        {
            return new ResetPasswordResult.InvalidToken();
        }

        return new ResetPasswordResult.InvalidPassword([.. result.Errors.Select(Translate)]);
    }

    public async Task<AccountToken?> CreateEmailConfirmationTokenAsync(string email, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.EmailConfirmed || user.Status != UserStatus.Active)
        {
            return null;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        return new AccountToken(user.Id, user.Email ?? email, user.DisplayName, token);
    }

    /// <summary>Identity messages in the language of the UI.</summary>
    private static string Translate(IdentityError error) => error.Code switch
    {
        "PasswordTooShort" => $"Das Passwort muss mindestens {AccountRules.PasswordMinLength} Zeichen lang sein.",
        "PasswordRequiresUpper" => "Das Passwort muss mindestens einen Großbuchstaben enthalten.",
        "PasswordRequiresLower" => "Das Passwort muss mindestens einen Kleinbuchstaben enthalten.",
        "PasswordRequiresDigit" => "Das Passwort muss mindestens eine Ziffer enthalten.",
        "PasswordRequiresNonAlphanumeric" => "Das Passwort muss mindestens ein Sonderzeichen enthalten.",
        "PasswordRequiresUniqueChars" => "Das Passwort muss mehr unterschiedliche Zeichen enthalten.",
        _ => error.Description,
    };
}
