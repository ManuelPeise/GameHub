using Logic.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Data.DataAccessor.Games;

public static class GameSessionsRegistration
{
    /// <summary>Registers the session repository. Requires <c>AddGameDatabase</c>.</summary>
    public static IServiceCollection AddGameSessionStore(this IServiceCollection services)
    {
        services.AddScoped<IGameSessionRepository, GameSessionRepository>();
        services.AddScoped<IStatisticsStore, Statistics.StatisticsStore>();
        return services;
    }
}
