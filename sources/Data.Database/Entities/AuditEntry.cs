namespace Data.Database.Entities;

/// <summary>Append-only audit log entry (table audit."AuditEntry"). Never updated or deleted.</summary>
public sealed class AuditEntry
{
    public long Id { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>User who acted or was affected; null for anonymous actions.</summary>
    public Guid? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? Target { get; set; }

    /// <summary>Success or Failure.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Additional details as JSON (jsonb), without personal data.</summary>
    public string? Details { get; set; }
}
