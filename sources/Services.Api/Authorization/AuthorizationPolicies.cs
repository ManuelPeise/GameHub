using Microsoft.AspNetCore.Authorization;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Services.Api.Authorization;

/// <summary>
/// Authorization policies of the API and the SignalR hubs. Rights are always checked on the server;
/// the frontend only hides what a user cannot use.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Signed-in, active user (any role).</summary>
    public const string Player = "Player";

    /// <summary>Role Administrator and, if App:Security:RequireAdminTwoFactor, a two-factor sign-in.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Claim set by ASP.NET Core Identity after a two-factor sign-in ("amr" = "mfa").</summary>
    public const string AuthenticationMethodClaim = "amr";
    public const string MultiFactorValue = "mfa";

    public static void Configure(AuthorizationOptions options, IAppSettings appSettings)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(appSettings);

        options.AddPolicy(Player, policy => policy.RequireAuthenticatedUser());

        options.AddPolicy(Administrator, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireRole(RoleNames.Administrator);

            if (appSettings.Security.RequireAdminTwoFactor)
            {
                policy.RequireClaim(AuthenticationMethodClaim, MultiFactorValue);
            }
        });
    }
}
