using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe.Ai;

public sealed class TicTacToeAiPlayer(TicTacToeModule module, IAiStrategy<TicTacToeView, PlaceMark> strategy) : IAiPlayer
{
    public string GameType => TicTacToeModule.Type;

    public string Difficulty => strategy.Difficulty;

    public string Version => strategy.Version;

    public (string ActionType, string? PayloadJson) ChooseAction(string stateJson, int seat, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);

        var state = module.DeserializeState(stateJson);
        var view = new TicTacToeView(
            state.Board,
            state.NextSeat,
            state.Status,
            state.WinnerSeat,
            state.WinningLine,
            [.. module.Rules.LegalActions(state, seat).Select(action => action.Cell)]);

        return module.SerializeAction(strategy.ChooseAction(view, seat, random));
    }
}
