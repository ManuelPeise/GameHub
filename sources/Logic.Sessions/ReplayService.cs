using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Shared.Models;

namespace Logic.Sessions;

/// <summary>
/// Rebuilds the state of a session from its seed and move log. Because every action uses the random
/// source derived from (seed, sequence), the replayed state equals the stored state.
/// </summary>
public sealed class ReplayService(GameModuleRegistry modules)
{
    /// <param name="upToSequence">Replay only up to this sequence (inclusive); null replays everything.</param>
    public string Replay(SessionSnapshot session, IEnumerable<GameActionRecord> actions, int? upToSequence = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        var steps = ReplaySteps(session, actions.Where(action => upToSequence is not { } limit || action.Sequence <= limit));
        return steps[^1].StateJson;
    }

    /// <summary>
    /// The state before the first action (sequence 0) and after every action, for a step-by-step replay.
    /// System events keep the state, except PlayerRemoved which may change it.
    /// </summary>
    public IReadOnlyList<ReplayStep> ReplaySteps(SessionSnapshot session, IEnumerable<GameActionRecord> actions)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(actions);

        var module = modules.Get(session.GameType);
        var seatOrders = session.Seats.ToDictionary(seat => seat.Id, seat => seat.Order);
        var state = module.CreateInitialState(session.Seats.Count, SeededRandom.ForAction(session.RngSeed, 0));
        var steps = new List<ReplayStep> { new(0, state) };

        foreach (var action in actions.OrderBy(a => a.Sequence))
        {
            state = ApplyStep(module, session, seatOrders, state, action);
            steps.Add(new ReplayStep(action.Sequence, state));
        }

        return steps;
    }

    private static string ApplyStep(
        Logic.Core.Abstractions.IGameModule module,
        SessionSnapshot session,
        Dictionary<Guid, int> seatOrders,
        string state,
        GameActionRecord action)
    {
        if (action.ActionType == SystemActionTypes.PlayerRemoved && action.SeatId is { } removedSeat)
        {
            // A removal can change the game state (e.g. skipping the seat), so it is replayed too.
            return module.RemoveSeat(state, seatOrders[removedSeat]);
        }

        if (SystemActionTypes.All.Contains(action.ActionType) || action.SeatId is not { } seatId)
        {
            return state;
        }

        return module.Apply(state, seatOrders[seatId], action.ActionType, action.PayloadJson,
                SeededRandom.ForAction(session.RngSeed, action.Sequence)) switch
            {
                ActionResult.Applied applied => applied.StateJson,
                ActionResult.Rejected rejected => throw new InvalidOperationException(
                    $"Replay of session {session.Id} failed at sequence {action.Sequence}: {rejected.Error}"),
                _ => throw new InvalidOperationException("Unknown action result."),
            };
    }
}

/// <summary>State after the action with the given sequence (0 = initial state).</summary>
public sealed record ReplayStep(int Sequence, string StateJson);
