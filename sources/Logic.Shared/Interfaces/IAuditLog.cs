using Logic.Shared.Models;

namespace Logic.Shared.Interfaces;

/// <summary>Port for the append-only audit log.</summary>
public interface IAuditLog
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken);
}
