using System.Text.Json;
using Logic.Core.Modules;
using Logic.TicTacToe.Domain;

namespace Logic.TicTacToe;

/// <summary>Tic-Tac-Toe as game module (action "PlaceMark" with payload {"cell": 0..8}).</summary>
public sealed class TicTacToeModule() : GameModule<TicTacToeState, PlaceMark>(new TicTacToeRules())
{
    /// <summary>Same value as Shared.Models.Games.GameTypes.TicTacToe (game projects do not reference Shared.Models).</summary>
    public const string Type = "TicTacToe";

    public override string GameType => Type;

    public override (string ActionType, string? PayloadJson) SerializeAction(PlaceMark action) =>
        (PlaceMark.ActionType, JsonSerializer.Serialize(action, JsonOptions));

    protected override PlaceMark? ParseAction(string actionType, string? payloadJson) =>
        actionType == PlaceMark.ActionType && !string.IsNullOrWhiteSpace(payloadJson)
            ? JsonSerializer.Deserialize<PlaceMark>(payloadJson, JsonOptions)
            : null;
}
