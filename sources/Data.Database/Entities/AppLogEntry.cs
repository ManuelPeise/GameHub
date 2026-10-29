namespace Data.Database.Entities;

/// <summary>One entry of the application log (table log."AppLog").</summary>
public sealed class AppLogEntry
{
    public long Id { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    /// <summary>Trace, Debug, Information, Warning, Error or Critical.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>Logger category, usually the full type name of the class that logged.</summary>
    public string Category { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Exception { get; set; }

    public int? EventId { get; set; }

    /// <summary>Structured log properties as JSON (jsonb).</summary>
    public string? Properties { get; set; }
}
