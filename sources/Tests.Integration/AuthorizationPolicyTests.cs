using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Api.Authorization;
using Shared.Models.Account;
using Shared.Models.Settings;

namespace Tests.Integration;

/// <summary>
/// Access rules of the API policies with and without role (US-007). End-to-end tests over HTTP with
/// Aspire.Hosting.Testing follow when the test environment for the database is set up.
/// </summary>
public sealed class AuthorizationPolicyTests
{
    private static IAuthorizationService CreateAuthorization(bool requireAdminTwoFactor = true)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["App:Security:RequireAdminTwoFactor"] = requireAdminTwoFactor ? "true" : "false",
            })
            .Build();
        var appSettings = new AppSettings(configuration);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization(options => AuthorizationPolicies.Configure(options, appSettings));

        return services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();
    }

    private static ClaimsPrincipal Anonymous() => new(new ClaimsIdentity());

    private static ClaimsPrincipal User(bool twoFactor, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.Add(new Claim(AuthorizationPolicies.AuthenticationMethodClaim, twoFactor ? "mfa" : "pwd"));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "Identity.Application"));
    }

    private static async Task<bool> IsAllowedAsync(IAuthorizationService authorization, ClaimsPrincipal user, string policy) =>
        (await authorization.AuthorizeAsync(user, resource: null, policy)).Succeeded;

    [Xunit.Fact]
    public async Task Anonymous_IsDenied_ForPlayerAndAdministrator()
    {
        var authorization = CreateAuthorization();

        Xunit.Assert.False(await IsAllowedAsync(authorization, Anonymous(), AuthorizationPolicies.Player));
        Xunit.Assert.False(await IsAllowedAsync(authorization, Anonymous(), AuthorizationPolicies.Administrator));
    }

    [Xunit.Fact]
    public async Task Player_IsAllowedAsPlayer_ButNotAsAdministrator()
    {
        var authorization = CreateAuthorization();
        var player = User(twoFactor: true, RoleNames.Player);

        Xunit.Assert.True(await IsAllowedAsync(authorization, player, AuthorizationPolicies.Player));
        Xunit.Assert.False(await IsAllowedAsync(authorization, player, AuthorizationPolicies.Administrator));
    }

    [Xunit.Fact]
    public async Task Administrator_WithTwoFactorSignIn_IsAllowed()
    {
        var authorization = CreateAuthorization();

        Xunit.Assert.True(await IsAllowedAsync(
            authorization, User(twoFactor: true, RoleNames.Administrator, RoleNames.Player), AuthorizationPolicies.Administrator));
    }

    [Xunit.Fact]
    public async Task Administrator_WithPasswordOnly_IsDenied_WhenTwoFactorIsRequired()
    {
        var authorization = CreateAuthorization(requireAdminTwoFactor: true);

        Xunit.Assert.False(await IsAllowedAsync(
            authorization, User(twoFactor: false, RoleNames.Administrator), AuthorizationPolicies.Administrator));
    }

    [Xunit.Fact]
    public async Task Administrator_WithPasswordOnly_IsAllowed_WhenTwoFactorIsNotRequired()
    {
        var authorization = CreateAuthorization(requireAdminTwoFactor: false);

        Xunit.Assert.True(await IsAllowedAsync(
            authorization, User(twoFactor: false, RoleNames.Administrator), AuthorizationPolicies.Administrator));
    }
}
