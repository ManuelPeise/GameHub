using Logic.Users;
using Shared.Models.Account;
using Shared.Models.Interfaces;

namespace Services.Api.Endpoints;

/// <summary>Account endpoints: registration and e-mail confirmation (US-005).</summary>
public static class AccountEndpoints
{
    public const string RegistrationRateLimitPolicy = "registration";

    private const string RegisteredMessage =
        "Fast geschafft! Wir haben dir eine E-Mail geschickt. Bitte bestätige darin deine Adresse, um dein Konto zu aktivieren.";

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/account");

        account.MapGet("/password-rules", () => new PasswordRulesResponse(AccountRules.PasswordRuleTexts));

        account.MapPost("/register", RegisterAsync)
            .RequireRateLimiting(RegistrationRateLimitPolicy);

        // Target of the link in the confirmation mail; redirects to the result page of Web.App.
        account.MapGet("/confirm-email", ConfirmEmailAsync);

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

    /// <summary>Address of Web.App for links: App:Environment:PublicBaseUrl, otherwise the requested host.</summary>
    private static string PublicBaseUrl(IAppSettings appSettings, HttpContext httpContext) =>
        string.IsNullOrWhiteSpace(appSettings.Environment.PublicBaseUrl)
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : appSettings.Environment.PublicBaseUrl.TrimEnd('/');
}
