using Logic.Shared.Models;
using Shared.Models.Administration;

namespace Logic.Shared.Interfaces;

/// <summary>Port for reading application and audit log (read only; both logs are never changed here).</summary>
public interface IAdminLogStore
{
    Task<AppLogPageDto> GetAppLogAsync(AppLogFilter filter, CancellationToken cancellationToken);

    Task<AuditPageDto> GetAuditLogAsync(AuditFilter filter, CancellationToken cancellationToken);
}
