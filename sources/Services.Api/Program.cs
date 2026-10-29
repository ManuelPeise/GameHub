using Services.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.MapDefaultEndpoints();

// Placeholder until endpoints and the SignalR hub arrive with their user stories.
// Reachable from Web.App through the Vite proxy as /api/info.
app.MapGet("/api/info", () => Results.Ok(new { name = "GameHub API" }));

app.Run();
