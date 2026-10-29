using System.Text.Json;
using Data.Database.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared.Models.Interfaces;

namespace Data.DataAccessor.Logging;

/// <summary>
/// Logging provider that writes entries from <c>App:Logging:DatabaseMinimumLevel</c> upwards into the
/// application log table. Console and OpenTelemetry (Aspire dashboard) keep receiving all entries.
/// </summary>
public sealed class DatabaseLoggerProvider(AppLogQueue queue, IAppSettings appSettings) : ILoggerProvider
{
    /// <summary>
    /// Categories that are never written to the database: database access itself and this logging
    /// code would otherwise log their own writes in an endless loop.
    /// </summary>
    private static readonly string[] ExcludedCategoryPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        typeof(DatabaseLoggerProvider).Namespace!,
    ];

    private readonly LogLevel _minimumLevel = Enum.Parse<LogLevel>(appSettings.Logging.DatabaseMinimumLevel);

    public ILogger CreateLogger(string categoryName)
    {
        foreach (var prefix in ExcludedCategoryPrefixes)
        {
            if (categoryName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return NullLogger.Instance;
            }
        }

        return new DatabaseLogger(categoryName, queue, _minimumLevel);
    }

    public void Dispose()
    {
    }

    private sealed class DatabaseLogger(string category, AppLogQueue queue, LogLevel minimumLevel) : ILogger
    {
        private const string OriginalFormatKey = "{OriginalFormat}";

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minimumLevel;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            queue.Enqueue(new AppLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = logLevel.ToString(),
                Category = category,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
                EventId = eventId.Id == 0 ? null : eventId.Id,
                Properties = SerializeProperties(state),
            });
        }

        private static string? SerializeProperties<TState>(TState state)
        {
            if (state is not IReadOnlyList<KeyValuePair<string, object?>> values || values.Count == 0)
            {
                return null;
            }

            var properties = new Dictionary<string, string?>(values.Count, StringComparer.Ordinal);
            foreach (var (key, value) in values)
            {
                if (key != OriginalFormatKey)
                {
                    properties[key] = value?.ToString();
                }
            }

            return properties.Count == 0 ? null : JsonSerializer.Serialize(properties);
        }
    }
}
