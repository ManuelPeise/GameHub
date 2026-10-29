using Data.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Shared.Models.Account;

namespace Data.DataAccessor.Users;

/// <summary>Creates administrator and development accounts (used by Services.DbTools).</summary>
public sealed class AccountSeeder(UserManager<User> userManager, TimeProvider timeProvider)
{
    /// <summary>Accounts seeded in Development: one administrator and two players.</summary>
    public static IReadOnlyList<(string Email, string DisplayName, bool IsAdministrator)> DevelopmentAccounts { get; } =
    [
        ("admin@gamehub.local", "Admin", true),
        ("spieler1@gamehub.local", "Spieler 1", false),
        ("spieler2@gamehub.local", "Spieler 2", false),
    ];

    /// <summary>
    /// Creates an active administrator (confirmed e-mail, roles Administrator and Player).
    /// If the account exists, only the role is added; the password stays unchanged.
    /// </summary>
    public async Task<SeedResult> EnsureAccountAsync(string email, string displayName, string password, bool isAdministrator)
    {
        var user = await userManager.FindByEmailAsync(email);
        var created = false;

        if (user is null)
        {
            user = new User
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                CreatedAt = timeProvider.GetUtcNow(),
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                return new SeedResult(false, false, [.. result.Errors.Select(error => error.Description)]);
            }

            created = true;
        }

        string[] roles = isAdministrator ? [RoleNames.Administrator, RoleNames.Player] : [RoleNames.Player];
        foreach (var role in roles)
        {
            if (!await userManager.IsInRoleAsync(user, role))
            {
                var added = await userManager.AddToRoleAsync(user, role);
                if (!added.Succeeded)
                {
                    return new SeedResult(created, false, [.. added.Errors.Select(error => error.Description)]);
                }
            }
        }

        return new SeedResult(created, true, []);
    }
}

/// <summary>Result of <see cref="AccountSeeder.EnsureAccountAsync"/>.</summary>
public sealed record SeedResult(bool Created, bool Succeeded, IReadOnlyList<string> Errors);
