using System.Threading.RateLimiting;
using Data.Database;
using Data.DataAccessor.Logging;
using Data.DataAccessor.Users;
using Logic.Users;
using Microsoft.AspNetCore.RateLimiting;
using Services.Api.Endpoints;
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
builder.AddUserAccounts();

builder.Services.AddScoped<RegistrationLogic>();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Registrations per client IP and hour (App:Security:RegistrationRateLimit).
    // Behind a reverse proxy the client IP needs forwarded headers (US-029).
    options.AddPolicy(AccountEndpoints.RegistrationRateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = appSettings.Security.RegistrationRateLimit,
                Window = TimeSpan.FromHours(1),
            }));
});

var app = builder.Build();

app.UseRateLimiter();

app.MapDefaultEndpoints();
app.MapAccountEndpoints();

// Reachable from Web.App through the Vite proxy as /api/info.
app.MapGet("/api/info", (IAppSettings settings) => Results.Ok(new
{
    name = "GameHub API",
    environment = settings.Environment.Name,
}));

app.Run();
