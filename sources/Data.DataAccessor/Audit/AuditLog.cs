using System.Text.Json;
using Data.Database;
using Data.Database.Entities;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;

namespace Data.DataAccessor.Audit;

/// <summary>Writes audit records to audit."AuditEntry". Entries are only ever added.</summary>
internal sealed class AuditLog(GameDbContext database, TimeProvider timeProvider) : IAuditLog
{
    public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(record);

        database.AuditEntries.Add(new AuditEntry
        {
            CreatedAt = timeProvider.GetUtcNow(),
            UserId = record.UserId,
            Action = record.Action,
            Target = record.Target,
            Result = record.Result.ToString(),
            Details = record.Details is { Count: > 0 } ? JsonSerializer.Serialize(record.Details) : null,
        });

        await database.SaveChangesAsync(cancellationToken);
    }
}
