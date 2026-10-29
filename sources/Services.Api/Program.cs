using Services.ServiceDefaults;
using Shared.Models.Interfaces;
using Shared.Models.Settings;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Read and validate the "App" section once; invalid settings stop the start with a clear message.
builder.Services.AddSingleton<IAppSettings>(new AppSettings(builder.Configuration));

var app = builder.Build();

app.MapDefaultEndpoints();

// Placeholder until endpoints and the SignalR hub arrive with their user stories.
// Reachable from Web.App through the Vite proxy as /api/info.
app.MapGet("/api/info", (IAppSettings settings) => Results.Ok(new
{
    name = "GameHub API",
    environment = settings.Environment.Name,
}));

app.Run();
