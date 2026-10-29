using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.AspNetCore.Identity;
using Shared.Models.Account;

namespace Data.DataAccessor.Users;

/// <summary><see cref="IUserAccountStore"/> on top of ASP.NET Core Identity.</summary>
internal sealed class UserAccountStore(UserManager<User> userManager, TimeProvider timeProvider) : IUserAccountStore
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
