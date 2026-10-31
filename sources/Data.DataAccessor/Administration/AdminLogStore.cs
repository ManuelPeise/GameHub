using Data.Database;
using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Shared.Models.Account;
using Shared.Models.Administration;

namespace Data.DataAccessor.Administration;

/// <summary>EF Core adapter for <see cref="IAdminLogStore"/>; paging by id (newest first).</summary>
public sealed class AdminLogStore(GameDbContext database) : IAdminLogStore
{
    private static readonly string[] Levels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    public async Task<AppLogPageDto> GetAppLogAsync(AppLogFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = database.AppLog.AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(entry => entry.Timestamp >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(entry => entry.Timestamp <= to);
        }

        var minimum = Array.IndexOf(Levels, filter.MinimumLevel ?? string.Empty);
        if (minimum > 0)
        {
            var allowed = Levels[minimum..];
            query = query.Where(entry => allowed.Contains(entry.Level));
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var pattern = Like(filter.Text);
            query = query.Where(entry => EF.Functions.ILike(entry.Message, pattern)
                || EF.Functions.ILike(entry.Category, pattern)
                || (entry.Exception != null && EF.Functions.ILike(entry.Exception, pattern)));
        }

        if (filter.BeforeId is { } beforeId)
        {
            query = query.Where(entry => entry.Id < beforeId);
        }

        var items = await query
            .OrderByDescending(entry => entry.Id)
            .Take(filter.Take + 1)
            .Select(entry => new AppLogEntryDto(
                entry.Id, entry.Timestamp, entry.Level, entry.Category, entry.Message, entry.Exception, entry.EventId, entry.Properties))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AppLogPageDto([.. items.Take(filter.Take)], items.Count > filter.Take ? items[filter.Take - 1].Id : null);
    }

    public async Task<AuditPageDto> GetAuditLogAsync(AuditFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);
        var query = database.AuditEntries.AsNoTracking();

        if (filter.From is { } from)
        {
            query = query.Where(entry => entry.CreatedAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(entry => entry.CreatedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Action))
        {
            var action = Like(filter.Action);
            query = query.Where(entry => EF.Functions.ILike(entry.Action, action));
        }

        if (!string.IsNullOrWhiteSpace(filter.User))
        {
            var pattern = Like(filter.User);
            var userIds = database.Users
                .Where(user => EF.Functions.ILike(user.DisplayName, pattern) || EF.Functions.ILike(user.Email!, pattern))
                .Select(user => user.Id);
            var targets = userIds.Select(id => id.ToString());
            query = query.Where(entry => (entry.UserId != null && userIds.Contains(entry.UserId.Value))
                || (entry.Target != null && targets.Contains(entry.Target)));
        }

        if (filter.BeforeId is { } beforeId)
        {
            query = query.Where(entry => entry.Id < beforeId);
        }

        var entries = await query
            .OrderByDescending(entry => entry.Id)
            .Take(filter.Take + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var page = entries.Take(filter.Take).ToList();

        // Current display names for users and user targets (deleted accounts show the placeholder name).
        var ids = page.Select(entry => entry.UserId).OfType<Guid>()
            .Concat(page.Select(entry => Guid.TryParse(entry.Target, out var target) ? target : (Guid?)null).OfType<Guid>())
            .Distinct()
            .ToList();
        var names = await database.Users.AsNoTracking()
            .Where(user => ids.Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => user.Status == Database.Entities.UserStatus.Deleted ? AccountRules.DeletedUserDisplayName : user.DisplayName,
                cancellationToken)
            .ConfigureAwait(false);

        return new AuditPageDto(
            [
                .. page.Select(entry => new AuditEntryDto(
                    entry.Id,
                    entry.CreatedAt,
                    entry.UserId,
                    entry.UserId is { } userId ? names.GetValueOrDefault(userId) : null,
                    entry.Action,
                    entry.Target,
                    Guid.TryParse(entry.Target, out var target) ? names.GetValueOrDefault(target) : null,
                    entry.Result,
                    entry.Details)),
            ],
            entries.Count > filter.Take ? page[^1].Id : null);
    }

    private static string Like(string text) =>
        $"%{text.Trim().Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
}
