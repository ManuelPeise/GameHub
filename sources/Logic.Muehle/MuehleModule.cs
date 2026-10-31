using System.Text.Json;
using Logic.Core.Modules;
using Logic.Muehle.Domain;

namespace Logic.Muehle;

/// <summary>Mühle as game module (action "MuehleMove" with payload {"from": 0..23 | null, "to": 0..23, "remove": 0..23 | null}).</summary>
public sealed class MuehleModule() : GameModule<MuehleState, MuehleMove>(new MuehleRules())
{
    /// <summary>Same value as Shared.Models.Games.GameTypes.Muehle (game projects do not reference Shared.Models).</summary>
    public const string Type = "Muehle";

    public override string GameType => Type;

    public override (string ActionType, string? PayloadJson) SerializeAction(MuehleMove action) =>
        (MuehleMove.ActionType, JsonSerializer.Serialize(action, JsonOptions));

    protected override MuehleMove? ParseAction(string actionType, string? payloadJson) =>
        actionType == MuehleMove.ActionType && !string.IsNullOrWhiteSpace(payloadJson)
            ? JsonSerializer.Deserialize<MuehleMove>(payloadJson, JsonOptions)
            : null;
}
