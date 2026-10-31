using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Administration;

namespace Logic.Administration;

/// <summary>Reading application and audit log for administrators (US-026), page by page.</summary>
public sealed class AdminLogLogic(IAdminLogStore store)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    public Task<AppLogPageDto> GetAppLogAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? minimumLevel,
        string? text,
        long? beforeId,
        int? take,
        CancellationToken cancellationToken) =>
        store.GetAppLogAsync(new AppLogFilter(from, to, minimumLevel, text, beforeId, PageSize(take)), cancellationToken);

    public Task<AuditPageDto> GetAuditLogAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? user,
        string? action,
        long? beforeId,
        int? take,
        CancellationToken cancellationToken) =>
        store.GetAuditLogAsync(new AuditFilter(from, to, user, action, beforeId, PageSize(take)), cancellationToken);

    private static int PageSize(int? take) => Math.Clamp(take ?? DefaultPageSize, 1, MaxPageSize);
}
