using Logic.Core.Model;

namespace Logic.Core.Abstractions;

/// <summary>
/// Rules of one game on typed state and actions. Pure: no UI, database, network or configuration.
/// Seats are positions in the turn order, starting at 0.
/// </summary>
public interface IGameRules<TState, TAction>
{
    int MinSeats { get; }

    int MaxSeats { get; }

    TState CreateInitialState(int seatCount, IRandom random);

    ValidationResult Validate(TState state, int seat, TAction action);

    /// <summary>Applies a validated action and returns the new state; the old state is not changed.</summary>
    TState Apply(TState state, int seat, TAction action, IRandom random);

    /// <summary>All valid actions of the seat (empty if it is not the seat's turn).</summary>
    IReadOnlyList<TAction> LegalActions(TState state, int seat);

    GameProgress GetProgress(TState state);
}
