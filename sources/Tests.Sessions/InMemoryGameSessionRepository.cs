using Logic.Shared.Interfaces;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Tests.Sessions;

/// <summary>In-memory fake of the session store with the same version check as the EF adapter.</summary>
internal sealed class InMemoryGameSessionRepository : IGameSessionRepository
{
    private readonly Dictionary<Guid, SessionSnapshot> _sessions = [];

    public List<GameActionRecord> Actions { get; } = [];

    public List<SeatResultWrite> Results { get; } = [];

    /// <summary>Simulates another action stored between load and save.</summary>
    public bool FailNextSave { get; set; }

    public void Add(SessionSnapshot session) => _sessions[session.Id] = session;

    public SessionSnapshot Get(Guid sessionId) => _sessions[sessionId];

    public Task<SessionSnapshot?> GetAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(sessionId, out var session) ? session : null);

    public Task<bool> SaveActionAsync(ActionWrite write, CancellationToken cancellationToken)
    {
        var session = _sessions[write.SessionId];
        if (FailNextSave || session.StateVersion != write.ExpectedVersion)
        {
            FailNextSave = false;
            return Task.FromResult(false);
        }

        Actions.Add(write.Action);
        Results.AddRange(write.Results);
        _sessions[write.SessionId] = session with
        {
            CurrentSeatId = write.CurrentSeatId,
            StateJson = write.NewStateJson,
            StateVersion = write.ExpectedVersion + 1,
            Status = write.NewStatus,
            LastSequence = write.Action.Sequence,
        };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<GameActionRecord>> GetActionsAsync(Guid sessionId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GameActionRecord>>([.. Actions.Where(a => a.SessionId == sessionId).OrderBy(a => a.Sequence)]);

    public Task<IReadOnlyList<Guid>> GetRunningSessionsWithAiTurnAsync(CancellationToken cancellationToken)
    {
        var result = _sessions.Values
            .Where(session => session.Status == SessionStatus.Running && session.CurrentSeatId is { } currentSeatId
                && session.Seats.Any(seat => seat.Id == currentSeatId && seat.Kind == SeatKind.Ai))
            .Select(session => session.Id)
            .ToList();
        return Task.FromResult<IReadOnlyList<Guid>>(result);
    }
}
