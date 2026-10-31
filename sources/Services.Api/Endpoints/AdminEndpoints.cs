using System.Security.Claims;
using Logic.Administration;
using Logic.Sessions.History;
using Logic.Users;
using Services.Api.Authorization;
using Shared.Models.Administration;

namespace Services.Api.Endpoints;

/// <summary>Admin area /api/admin; every endpoint requires the policy Administrator.</summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var admin = app.MapGroup("/api/admin")
            .RequireAuthorization(AuthorizationPolicies.Administrator);

        // Access check for Web.App.
        admin.MapGet("/status", () => Results.Ok(new { access = true }));

        // Player administration (US-025).
        admin.MapGet("/users", async (
                string? query,
                bool? includeDeleted,
                int? skip,
                int? take,
                PlayerAdministrationLogic logic,
                ClaimsPrincipal principal,
                CancellationToken cancellationToken) =>
            AdminId(principal) is { } adminId
                ? Results.Ok(await logic.SearchAsync(adminId, query, includeDeleted ?? false, skip ?? 0, take ?? 25, cancellationToken))
                : Results.Unauthorized());

        admin.MapPost("/users/{id:guid}/lock", async (Guid id, PlayerAdministrationLogic logic, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            AdminId(principal) is { } adminId ? ToResult(await logic.LockAsync(adminId, id, cancellationToken)) : Results.Unauthorized());

        admin.MapPost("/users/{id:guid}/unlock", async (Guid id, PlayerAdministrationLogic logic, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
            AdminId(principal) is { } adminId ? ToResult(await logic.UnlockAsync(adminId, id, cancellationToken)) : Results.Unauthorized());

        admin.MapPost("/users/{id:guid}/delete", async (
                Guid id,
                AdminDeleteUserRequest request,
                PlayerAdministrationLogic logic,
                ClaimsPrincipal principal,
                CancellationToken cancellationToken) =>
            AdminId(principal) is { } adminId
                ? ToResult(await logic.DeleteAsync(adminId, id, request.Confirmed, cancellationToken))
                : Results.Unauthorized());

        // Logs and games (US-026).
        admin.MapGet("/logs/app", async (
                DateTimeOffset? from,
                DateTimeOffset? to,
                string? level,
                string? text,
                long? beforeId,
                int? take,
                AdminLogLogic logic,
                CancellationToken cancellationToken) =>
            Results.Ok(await logic.GetAppLogAsync(from, to, level, text, beforeId, take, cancellationToken)));

        admin.MapGet("/logs/audit", async (
                DateTimeOffset? from,
                DateTimeOffset? to,
                string? user,
                string? action,
                long? beforeId,
                int? take,
                AdminLogLogic logic,
                CancellationToken cancellationToken) =>
            Results.Ok(await logic.GetAuditLogAsync(from, to, user, action, beforeId, take, cancellationToken)));

        admin.MapGet("/games", async (string? query, int? skip, int? take, GameHistoryLogic logic, CancellationToken cancellationToken) =>
            Results.Ok(await logic.SearchForAdminAsync(query, skip ?? 0, take ?? 25, cancellationToken)));

        admin.MapGet("/games/{id:guid}", async (Guid id, GameHistoryLogic logic, CancellationToken cancellationToken) =>
            await logic.GetDetailForAdminAsync(id, cancellationToken) is { } detail
                ? Results.Ok(detail)
                : Results.NotFound(new { error = "Die Partie wurde nicht gefunden." }));

        return app;
    }

    private static IResult ToResult(AdminActionResult result) => result switch
    {
        AdminActionResult.Done => Results.NoContent(),
        AdminActionResult.NotFound => Results.NotFound(new { error = "Der Spieler wurde nicht gefunden." }),
        AdminActionResult.Self => Results.Conflict(new { error = "Das eigene Konto kann hier nicht gesperrt oder gelöscht werden." }),
        AdminActionResult.LastAdministrator => Results.Conflict(new { error = "Der letzte aktive Administrator kann nicht gesperrt oder gelöscht werden." }),
        AdminActionResult.NotConfirmed => Results.UnprocessableEntity(new { error = "Bitte bestätige das Löschen." }),
        _ => Results.Conflict(new { error = "Diese Aktion ist für den Spieler gerade nicht möglich." }),
    };

    private static Guid? AdminId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
