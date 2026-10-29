using System.Security.Claims;
using Logic.Sessions;
using Logic.Shared.Models;
using Shared.Models.Games;
using Shared.Models.Interfaces;
using Services.Api.Authorization;

namespace Services.Api.Endpoints;

public static class GameSessionEndpoints
{
    public static IEndpointRouteBuilder MapGameSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions").RequireAuthorization(AuthorizationPolicies.Player);

        sessions.MapPost(string.Empty, CreateAsync);
        sessions.MapGet("/{id:guid}", GetAsync);
        sessions.MapGet("/open", ListOpenLobbiesAsync);
        sessions.MapPost("/join", JoinAsync);
        sessions.MapPost("/{id:guid}/join", JoinByIdAsync);
        sessions.MapPost("/{id:guid}/ai-seats", AddAiSeatAsync);
        sessions.MapDelete("/{id:guid}/seats/{seatId:guid}", RemoveAiSeatAsync);
        sessions.MapPost("/{id:guid}/start", StartAsync);
        sessions.MapPost("/{id:guid}/rematch", RematchAsync);
        sessions.MapPost("/{id:guid}/actions", SubmitActionAsync);

        return app;
    }

    private static async Task<IResult> CreateAsync(
        CreateSessionRequest request,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await logic.CreateAsync(userId.Value, request, cancellationToken);
        return ToSessionResult(appSettings, outcome);
    }

    private static async Task<IResult> GetAsync(
        Guid id,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await logic.GetAsync(userId.Value, id, cancellationToken);
        return ToSessionResult(appSettings, outcome);
    }

    private static async Task<IResult> JoinAsync(
        JoinSessionRequest request,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await logic.JoinAsync(userId.Value, request, cancellationToken);
        return ToSessionResult(appSettings, outcome);
    }

    private static async Task<IResult> ListOpenLobbiesAsync(
        string? gameType,
        SessionLifecycleLogic logic,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(await logic.ListOpenLobbiesAsync(userId.Value, gameType, cancellationToken));
    }

    private static async Task<IResult> JoinByIdAsync(
        Guid id,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return ToSessionResult(appSettings, await logic.JoinByIdAsync(userId.Value, id, cancellationToken));
    }

    private static async Task<IResult> AddAiSeatAsync(
        Guid id,
        AddAiSeatRequest request,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return ToSessionResult(appSettings, await logic.AddAiSeatAsync(userId.Value, id, request, cancellationToken));
    }

    private static async Task<IResult> RemoveAiSeatAsync(
        Guid id,
        Guid seatId,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        return ToSessionResult(appSettings, await logic.RemoveAiSeatAsync(userId.Value, id, seatId, cancellationToken));
    }

    private static async Task<IResult> StartAsync(
        Guid id,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await logic.StartAsync(userId.Value, id, cancellationToken);
        return ToSessionResult(appSettings, outcome);
    }

    private static async Task<IResult> RematchAsync(
        Guid id,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var outcome = await logic.RematchAsync(userId.Value, id, cancellationToken);
        return ToSessionResult(appSettings, outcome);
    }

    private static async Task<IResult> SubmitActionAsync(
        Guid id,
        SubmitSessionActionRequest request,
        SessionLifecycleLogic logic,
        IAppSettings appSettings,
        SubmitActionHandler handler,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var access = await logic.GetAsync(userId.Value, id, cancellationToken);
        if (access is not SessionLifecycleOutcome.Success allowed)
        {
            return ToSessionResult(appSettings, access);
        }

        var result = await handler.HandleAsync(
            new SubmitActionCommand(id, allowed.MySeatId, request.ExpectedVersion, request.ActionType, request.Payload),
            cancellationToken);

        return result switch
        {
            SubmitActionResult.Applied applied => Results.Ok(applied),
            SubmitActionResult.Rejected rejected when rejected.Kind == SubmitRejection.NotFound =>
                Results.NotFound(new { error = rejected.Error }),
            SubmitActionResult.Rejected rejected when rejected.Kind == SubmitRejection.Conflict =>
                Results.Conflict(new { error = rejected.Error }),
            SubmitActionResult.Rejected rejected =>
                Results.UnprocessableEntity(new { error = rejected.Error }),
            _ => Results.UnprocessableEntity(new { error = "Die Aktion konnte nicht verarbeitet werden." }),
        };
    }

    private static IResult ToSessionResult(IAppSettings appSettings, SessionLifecycleOutcome outcome) => outcome switch
    {
        SessionLifecycleOutcome.Success success => Results.Ok(ToDto(success, appSettings.Game.TurnTimeout)),
        SessionLifecycleOutcome.Failure failure when failure.Kind == SessionLifecycleFailureKind.NotFound =>
            Results.NotFound(new { error = failure.Error }),
        SessionLifecycleOutcome.Failure failure when failure.Kind == SessionLifecycleFailureKind.Forbidden =>
            Results.StatusCode(StatusCodes.Status403Forbidden),
        SessionLifecycleOutcome.Failure failure when failure.Kind == SessionLifecycleFailureKind.Conflict =>
            Results.Conflict(new { error = failure.Error }),
        SessionLifecycleOutcome.Failure failure =>
            Results.UnprocessableEntity(new { error = failure.Error }),
        _ => Results.UnprocessableEntity(),
    };

    private static SessionDto ToDto(SessionLifecycleOutcome.Success success, TimeSpan turnTimeout) =>
        new(
            success.Session.Id,
            success.Session.GameType,
            success.Session.Mode,
            success.Session.Status,
            success.Session.StateVersion,
            success.Session.StateJson,
            [.. success.Session.Seats.Select(seat => new SeatDto(
                seat.Id,
                seat.Order,
                seat.Kind,
                seat.DisplayName,
                seat.Symbol,
                seat.UserId,
                seat.ConnectionStatus))],
            success.Session.InviteCode,
            success.MySeatId,
            success.Session.CurrentSeatId,
            TurnDeadline(success.Session, turnTimeout));

    /// <summary>Deadline of the seat at turn; only for connected humans in running multiplayer games.</summary>
    private static DateTimeOffset? TurnDeadline(SessionSnapshot session, TimeSpan turnTimeout) =>
        session is { Mode: GameMode.Multiplayer, Status: SessionStatus.Running, TurnStartedAt: { } startedAt }
        && session.Seats.Any(seat => seat.Id == session.CurrentSeatId
            && seat.Kind == SeatKind.Human
            && seat.ConnectionStatus == SeatConnectionStatus.Connected)
            ? startedAt + turnTimeout
            : null;

    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;
}
