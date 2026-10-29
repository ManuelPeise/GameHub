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
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(actions);

        var module = modules.Get(session.GameType);
        var seatOrders = session.Seats.ToDictionary(seat => seat.Id, seat => seat.Order);
        var state = module.CreateInitialState(session.Seats.Count, SeededRandom.ForAction(session.RngSeed, 0));

        foreach (var action in actions.OrderBy(a => a.Sequence))
        {
            if (upToSequence is { } limit && action.Sequence > limit)
            {
                break;
            }

            if (SystemActionTypes.All.Contains(action.ActionType) || action.SeatId is not { } seatId)
            {
                continue;
            }

            var result = module.Apply(state, seatOrders[seatId], action.ActionType, action.PayloadJson,
                SeededRandom.ForAction(session.RngSeed, action.Sequence));
            state = result switch
            {
                ActionResult.Applied applied => applied.StateJson,
                ActionResult.Rejected rejected => throw new InvalidOperationException(
                    $"Replay of session {session.Id} failed at sequence {action.Sequence}: {rejected.Error}"),
                _ => throw new InvalidOperationException("Unknown action result."),
            };
        }

        return state;
    }
}
