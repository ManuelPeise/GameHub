using Services.Api.Authorization;

namespace Services.Api.Endpoints;

/// <summary>Admin area /api/admin; every endpoint requires the policy Administrator.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        // Access check for Web.App; the admin functions follow with US-025 to US-028.
        admin.MapGet("/status", () => Results.Ok(new { access = true }));

        return app;
    }
}
