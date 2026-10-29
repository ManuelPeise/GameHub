using System.Security.Claims;
using Logic.Sessions.History;
using Services.Api.Authorization;

namespace Services.Api.Endpoints;

public static class GameHistoryEndpoints
{
    public static IEndpointRouteBuilder MapGameHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        var history = app.MapGroup("/api/history").RequireAuthorization(AuthorizationPolicies.Player);

        history.MapGet(string.Empty, ListAsync);
        history.MapGet("/{id:guid}", GetAsync);

        return app;
    }

    private static async Task<IResult> ListAsync(
        int? skip,
        int? take,
        GameHistoryLogic logic,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await logic.ListAsync(userId, skip ?? 0, take ?? 20, cancellationToken));
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        GameHistoryLogic logic,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return Results.Unauthorized();
        }

        var detail = await logic.GetDetailAsync(userId, id, cancellationToken);
        return detail is null
            ? Results.NotFound(new { error = "Die Partie wurde nicht gefunden." })
            : Results.Ok(detail);
    }
}
