using System.Security.Claims;
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

        if (result.RequiresTwoFactor)
        {
            // Identity has stored the user in the two-factor cookie; the code follows with TwoFactorSignInAsync.
            return new LoginOutcome(LoginResult.RequiresTwoFactor, user.Id);
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

    public async Task<LoginOutcome> TwoFactorSignInAsync(string code, bool persistent, CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            // No (or an expired) first login step.
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(NormalizeCode(code), persistent, rememberClient: false);
        return await CompleteSecondFactorAsync(user, result);
    }

    public async Task<LoginOutcome> RecoveryCodeSignInAsync(string recoveryCode, CancellationToken cancellationToken)
    {
        var user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return new LoginOutcome(LoginResult.InvalidCredentials, null);
        }

        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(NormalizeCode(recoveryCode));
        return await CompleteSecondFactorAsync(user, result);
    }

    public async Task<TwoFactorSetup?> GetTwoFactorSetupAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return null;
        }

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user) ?? string.Empty;
        }

        const string issuer = "GameHub";
        var uri = $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(user.Email ?? string.Empty)}"
            + $"?secret={key}&issuer={Uri.EscapeDataString(issuer)}&digits=6";

        return new TwoFactorSetup(user.TwoFactorEnabled, key, uri);
    }

    public async Task<IReadOnlyList<string>?> EnableTwoFactorAsync(Guid userId, string code, CancellationToken cancellationToken)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.Status != UserStatus.Active)
        {
            return null;
        }

        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            userManager.Options.Tokens.AuthenticatorTokenProvider,
            NormalizeCode(code));
        if (!valid)
        {
            return null;
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

        // The current cookie is a password-only sign-in; renew it as two-factor sign-in ("amr" = "mfa"),
        // so administrators can use the admin area right away.
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);

        return [.. recoveryCodes ?? []];
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
        return new AccountInfo(user.Id, user.Email ?? string.Empty, user.DisplayName, [.. roles], user.TwoFactorEnabled);
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

    private async Task<LoginOutcome> CompleteSecondFactorAsync(User user, SignInResult result)
    {
        if (result.Succeeded)
        {
            user.LastLoginAt = timeProvider.GetUtcNow();
            await userManager.UpdateAsync(user);
            return new LoginOutcome(LoginResult.Succeeded, user.Id);
        }

        return result.IsLockedOut
            ? new LoginOutcome(LoginResult.LockedOut, user.Id)
            : new LoginOutcome(LoginResult.InvalidCredentials, user.Id);
    }

    /// <summary>Codes are often typed with spaces or dashes ("123 456", "ab12-cd34").</summary>
    private static string NormalizeCode(string? code) =>
        (code ?? string.Empty).Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal);

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
