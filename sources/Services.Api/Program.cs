using System.Threading.RateLimiting;
using Data.Database;
using Data.DataAccessor.Games;
using Data.DataAccessor.Logging;
using Data.DataAccessor.Users;
using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.Sessions;
using Logic.Sessions.Ai;
using Logic.Shared.Interfaces;
using Logic.TicTacToe;
using Logic.TicTacToe.Ai;
using Logic.Users;
using Microsoft.AspNetCore.RateLimiting;
using Services.Api.Authorization;
using Services.Api.Endpoints;
using Services.Api.Games;
using Services.ServiceDefaults;
using Shared.Models.Interfaces;
using Shared.Models.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Read and validate the "App" section once; invalid settings stop the start with a clear message.
var appSettings = new AppSettings(builder.Configuration);
builder.Services.AddSingleton<IAppSettings>(appSettings);

builder.AddGameDatabase();
builder.AddDatabaseLogging();
builder.AddUserAccounts(appSettings);

builder.Services.AddAuthorization(options => AuthorizationPolicies.Configure(options, appSettings));

builder.Services.AddScoped<AccountMailer>();
builder.Services.AddScoped<RegistrationLogic>();
builder.Services.AddScoped<LoginLogic>();
builder.Services.AddScoped<AccountRecoveryLogic>();
builder.Services.AddScoped<TwoFactorLogic>();
builder.Services.AddScoped<AccountDeletionLogic>();

// Games: modules, the single move path and replay (endpoints follow with US-012/US-013).
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TicTacToeModule>();
builder.Services.AddSingleton<IGameModule>(services => services.GetRequiredService<TicTacToeModule>());
builder.Services.AddSingleton<IAiStrategy<TicTacToeView, Logic.TicTacToe.Domain.PlaceMark>, RandomTicTacToeStrategy>();
builder.Services.AddSingleton<IAiPlayer, TicTacToeAiPlayer>();
builder.Services.AddSingleton<IAiStrategyRegistry, AiStrategyRegistry>();
builder.Services.AddSingleton<IAiTurnScheduler, AiTurnScheduler>();
builder.Services.AddSingleton<GameModuleRegistry>();
builder.Services.AddSingleton<ReplayService>();
builder.Services.AddSingleton<IGameEventPublisher, NullGameEventPublisher>();
builder.Services.AddGameSessionStore();
builder.Services.AddScoped<SubmitActionHandler>();
builder.Services.AddScoped<SessionLifecycleLogic>();
builder.Services.AddScoped<AiTurnRunner>();
builder.Services.AddHostedService<AiTurnWorker>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Per client IP. Behind a reverse proxy the client IP needs forwarded headers (US-029).
    options.AddPolicy(AccountEndpoints.RegistrationRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = appSettings.Security.RegistrationRateLimit,
                Window = TimeSpan.FromHours(1),
            }));

    options.AddPolicy(AccountEndpoints.LoginRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = appSettings.Security.LoginRateLimit,
                Window = TimeSpan.FromMinutes(1),
            }));
});

var app = builder.Build();

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();
app.MapAccountEndpoints();
app.MapAdminEndpoints();
app.MapGameSessionEndpoints();

// Reachable from Web.App through the Vite proxy as /api/info.
app.MapGet("/api/info", (IAppSettings settings) => Results.Ok(new
{
    name = "GameHub API",
    environment = settings.Environment.Name,
}));

app.Run();

static string ClientKey(HttpContext httpContext) => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
