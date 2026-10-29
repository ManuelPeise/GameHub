var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

// Placeholder until endpoints and the SignalR hub arrive with their user stories.
app.MapGet("/", () => "GameHub API");

app.Run();
