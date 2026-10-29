using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Data.DataAccessor.Logging;

public static class DatabaseLoggingRegistration
{
    /// <summary>
    /// Adds the application log in the database (US-004). Requires <c>IAppSettings</c> and the
    /// GameDbContext (<c>AddGameDatabase</c>) to be registered.
    /// </summary>
    public static IHostApplicationBuilder AddDatabaseLogging(this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AppLogQueue>();
        builder.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();
        builder.Services.AddHostedService<AppLogWriter>();
        builder.Services.AddHostedService<AppLogCleanupJob>();

        return builder;
    }
}
