using Logic.Core.Model;

namespace Logic.Core.Abstractions;

/// <summary>
/// A game as seen by the application: state and actions as JSON, so sessions, move log and transport
/// need no knowledge of the game. Implemented with <see cref="Modules.GameModule{TState, TAction}"/>.
/// </summary>
public interface IGameModule
{
    /// <summary>Game type stored with the session, e.g. "TicTacToe".</summary>
    string GameType { get; }

    int MinSeats { get; }

    int MaxSeats { get; }

    string CreateInitialState(int seatCount, IRandom random);

    /// <summary>Validates and applies an action; rejected actions change nothing.</summary>
    ActionResult Apply(string stateJson, int seat, string actionType, string? payloadJson, IRandom random);

    /// <summary>All currently legal actions of the given seat in transport format.</summary>
    IReadOnlyList<(string ActionType, string? PayloadJson)> GetLegalActions(string stateJson, int seat);

    GameProgress GetProgress(string stateJson);

    /// <summary>State after removing a seat, see <see cref="IGameRules{TState, TAction}.RemoveSeat"/>.</summary>
    string RemoveSeat(string stateJson, int seat);
}
