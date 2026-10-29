using System.Security.Claims;
using Logic.Shared.Models;
using Logic.Users;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Services.Api.Endpoints;

/// <summary>Account endpoints: registration, e-mail confirmation (US-005), login and recovery (US-006).</summary>
public static class AccountEndpoints
{
    public const string RegistrationRateLimitPolicy = "registration";
    public const string LoginRateLimitPolicy = "login";

    private const string RegisteredMessage =
        "Fast geschafft! Wir haben dir eine E-Mail geschickt. Bitte bestätige darin deine Adresse, um dein Konto zu aktivieren.";

    private const string PasswordResetRequestedMessage =
        "Wenn ein Konto mit dieser Adresse existiert, haben wir dir einen Link zum Zurücksetzen des Passworts geschickt.";

    private const string ConfirmationResentMessage =
        "Wenn ein unbestätigtes Konto mit dieser Adresse existiert, haben wir dir einen neuen Bestätigungslink geschickt.";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account");

        account.MapGet("/password-rules", () => new PasswordRulesResponse(AccountRules.PasswordRuleTexts));

        account.MapPost("/register", RegisterAsync).RequireRateLimiting(RegistrationRateLimitPolicy);

        // Target of the link in the confirmation mail; redirects to the result page of Web.App.
        account.MapGet("/confirm-email", ConfirmEmailAsync);

        account.MapPost("/login", LoginAsync).RequireRateLimiting(LoginRateLimitPolicy);
        account.MapPost("/logout", LogoutAsync);
        account.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        account.MapPost("/forgot-password", ForgotPasswordAsync).RequireRateLimiting(LoginRateLimitPolicy);
        account.MapPost("/reset-password", ResetPasswordAsync).RequireRateLimiting(LoginRateLimitPolicy);
        account.MapPost("/resend-confirmation", ResendConfirmationAsync).RequireRateLimiting(LoginRateLimitPolicy);

        return app;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegistrationLogic registration,
        IAppSettings appSettings,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var outcome = await registration.RegisterAsync(request, PublicBaseUrl(appSettings, httpContext), cancellationToken);

        return outcome switch
        {
            RegistrationOutcome.Invalid invalid =>
                Results.ValidationProblem(invalid.Errors.ToDictionary(error => error.Key, error => error.Value)),
            _ => Results.Accepted(value: new RegisterResponse(RegisteredMessage)),
        };
    }

    private static async Task<IResult> ConfirmEmailAsync(
        Guid userId,
        string token,
        RegistrationLogic registration,
        IAppSettings appSettings,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var confirmed = await registration.ConfirmEmailAsync(userId, token, cancellationToken);

        return Results.Redirect(
            $"{PublicBaseUrl(appSettings, httpContext)}/email-confirmed?success={(confirmed ? "true" : "false")}");
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginLogic login,
        CancellationToken cancellationToken)
    {
        var outcome = await login.LoginAsync(request, cancellationToken);

        if (outcome.Result == LoginResult.Succeeded && outcome.UserId is { } userId)
        {
            var user = await login.GetCurrentUserAsync(userId, cancellationToken);
            return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
        }

        var (code, message) = outcome.Result switch
        {
            LoginResult.LockedOut => (LoginErrorCodes.LockedOut,
                "Zu viele fehlgeschlagene Versuche. Dein Konto ist vorübergehend gesperrt, bitte versuche es später erneut."),
            LoginResult.EmailNotConfirmed => (LoginErrorCodes.EmailNotConfirmed,
                "Bitte bestätige zuerst deine E-Mail-Adresse. Du kannst dir den Bestätigungslink erneut schicken lassen."),
            LoginResult.AccountDisabled => (LoginErrorCodes.AccountDisabled,
                "Dein Konto ist gesperrt. Bitte wende dich an den Betreiber."),
            _ => (LoginErrorCodes.InvalidCredentials, "E-Mail-Adresse oder Passwort ist falsch."),
        };

        return Results.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: message,
            extensions: new Dictionary<string, object?> { ["code"] = code });
    }

    private static async Task<IResult> LogoutAsync(
        LoginLogic login,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        await login.LogoutAsync(GetUserId(principal), cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        LoginLogic login,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(principal);
        var user = userId is null ? null : await login.GetCurrentUserAsync(userId.Value, cancellationToken);

        // A cookie of a meanwhile locked or deleted account is treated as signed out.
        return user is null ? Results.Unauthorized() : Results.Ok(ToResponse(user));
    }

    private static async Task<IResult> ForgotPasswordAsync(
        EmailRequest request,
        AccountRecoveryLogic recovery,
        IAppSettings appSettings,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await recovery.RequestPasswordResetAsync(request.Email, PublicBaseUrl(appSettings, httpContext), cancellationToken);
        return Results.Accepted(value: new MessageResponse(PasswordResetRequestedMessage));
    }

    private static async Task<IResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        AccountRecoveryLogic recovery,
        CancellationToken cancellationToken)
    {
        var result = await recovery.ResetPasswordAsync(request, cancellationToken);

        return result switch
        {
            ResetPasswordResult.Succeeded => Results.NoContent(),
            ResetPasswordResult.InvalidPassword invalid => Results.ValidationProblem(
                new Dictionary<string, string[]> { ["newPassword"] = [.. invalid.Errors] }),
            _ => Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["token"] = ["Der Link ist ungültig oder abgelaufen. Bitte fordere einen neuen an."],
                }),
        };
    }

    private static async Task<IResult> ResendConfirmationAsync(
        EmailRequest request,
        AccountRecoveryLogic recovery,
        IAppSettings appSettings,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        await recovery.ResendConfirmationAsync(request.Email, PublicBaseUrl(appSettings, httpContext), cancellationToken);
        return Results.Accepted(value: new MessageResponse(ConfirmationResentMessage));
    }

    private static CurrentUserResponse ToResponse(AccountInfo user) =>
        new(user.Id, user.Email, user.DisplayName, user.Roles);

    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) ? userId : null;

    /// <summary>Address of Web.App for links: App:Environment:PublicBaseUrl, otherwise the requested host.</summary>
    private static string PublicBaseUrl(IAppSettings appSettings, HttpContext httpContext) =>
        string.IsNullOrWhiteSpace(appSettings.Environment.PublicBaseUrl)
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : appSettings.Environment.PublicBaseUrl.TrimEnd('/');
}
