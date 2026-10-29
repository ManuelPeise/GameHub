using System.Security.Claims;
using Logic.Sessions.Statistics;
using Services.Api.Authorization;

namespace Services.Api.Endpoints;

public static class StatisticsEndpoints
{
    public static IEndpointRouteBuilder MapStatisticsEndpoints(this IEndpointRouteBuilder app)
    {
        var statistics = app.MapGroup("/api/statistics").RequireAuthorization(AuthorizationPolicies.Player);

        statistics.MapGet("/me", async (StatisticsLogic logic, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            UserId(principal) is { } userId
                ? Results.Ok(await logic.GetPersonalAsync(userId, cancellationToken))
                : Results.Unauthorized());

        statistics.MapGet("/leaderboard", async (
            string gameType,
            string? period,
            StatisticsLogic logic,
            ClaimsPrincipal principal,
            CancellationToken cancellationToken) =>
            UserId(principal) is { } userId
                ? Results.Ok(await logic.GetLeaderboardAsync(userId, gameType, period, cancellationToken))
                : Results.Unauthorized());

        return app;
    }

    private static Guid? UserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
