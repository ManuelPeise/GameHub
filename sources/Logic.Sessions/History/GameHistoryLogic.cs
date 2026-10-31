using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Sessions.History;

/// <summary>Own finished games with their move log and a step-by-step replay.</summary>
public sealed class GameHistoryLogic(IGameSessionRepository repository, ReplayService replay)
{
    public const int MaxPageSize = 50;

    public async Task<IReadOnlyList<GameHistoryEntryDto>> ListAsync(Guid userId, int skip, int take, CancellationToken cancellationToken)
    {
        var records = await repository
            .GetHistoryAsync(userId, Math.Max(0, skip), Math.Clamp(take, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return [.. records.Select(record => ToEntry(record, userId))];
    }

    /// <summary>
    /// Move log with the state after every action. Null if the game does not exist, is still open or the
    /// user did not take part (administrators get access to all games with US-026).
    /// </summary>
    public async Task<GameHistoryDetailDto?> GetDetailAsync(Guid userId, Guid sessionId, CancellationToken cancellationToken)
    {
        var record = await repository.GetHistoryRecordAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (record is null
            || record.Status is not (SessionStatus.Finished or SessionStatus.Aborted)
            || record.Seats.All(seat => seat.UserId != userId))
        {
            return null;
        }

        return await BuildDetailAsync(record, userId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Administrators (US-026): every game except lobbies, also while it is running.</summary>
    public async Task<GameHistoryDetailDto?> GetDetailForAdminAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var record = await repository.GetHistoryRecordAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return record is null || record.Status == SessionStatus.Lobby
            ? null
            : await BuildDetailAsync(record, Guid.Empty, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Administrators: search by session id or player name/e-mail.</summary>
    public async Task<IReadOnlyList<global::Shared.Models.Administration.AdminGameDto>> SearchForAdminAsync(
        string? query,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var records = await repository
            .SearchGamesAsync(query, Math.Max(0, skip), Math.Clamp(take, 1, MaxPageSize), cancellationToken)
            .ConfigureAwait(false);
        return
        [
            .. records.Select(record => new global::Shared.Models.Administration.AdminGameDto(
                record.SessionId,
                record.GameType,
                record.Mode,
                record.Status,
                record.StartedAt,
                record.EndedAt,
                [.. record.Seats.OrderBy(seat => seat.Order).Select(seat => seat.DisplayName)])),
        ];
    }

    private async Task<GameHistoryDetailDto?> BuildDetailAsync(HistoryRecord record, Guid userId, CancellationToken cancellationToken)
    {
        var sessionId = record.SessionId;

        var session = await repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return null;
        }

        var actions = await repository.GetActionsAsync(sessionId, cancellationToken).ConfigureAwait(false);
        var steps = replay.ReplaySteps(session, actions);
        var stateBySequence = steps.ToDictionary(step => step.Sequence, step => step.StateJson);

        return new GameHistoryDetailDto(
            ToEntry(record, userId),
            steps[0].StateJson,
            [
                .. actions.Select(action => new HistoryActionDto(
                    action.Sequence,
                    action.SeatId,
                    action.ActionType,
                    action.PayloadJson,
                    action.RandomResultJson,
                    action.AiStrategyVersion,
                    action.CreatedAt,
                    stateBySequence[action.Sequence])),
            ]);
    }

    private static GameHistoryEntryDto ToEntry(HistoryRecord record, Guid userId)
    {
        var mine = record.Seats.FirstOrDefault(seat => seat.UserId == userId);
        return new GameHistoryEntryDto(
            record.SessionId,
            record.GameType,
            record.Mode,
            record.Status,
            record.StartedAt,
            record.EndedAt,
            mine?.Outcome,
            mine?.Placement,
            [
                .. record.Seats.Select(seat => new HistorySeatDto(
                    seat.SeatId,
                    seat.Order,
                    seat.DisplayName,
                    seat.Kind,
                    seat.UserId == userId,
                    seat.Outcome,
                    seat.Placement,
                    seat.Score)),
            ]);
    }
}
