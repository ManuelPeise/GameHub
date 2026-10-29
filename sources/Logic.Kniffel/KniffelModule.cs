using System.Text.Json;
using Logic.Core.Modules;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel;

/// <summary>Kniffel as game module: actions "Roll" ({"heldMask": 0..31}) and "Score" ({"category": "FullHouse"}).</summary>
public sealed class KniffelModule() : GameModule<KniffelState, KniffelAction>(new KniffelRules())
{
    /// <summary>Same value as Shared.Models.Games.GameTypes.Kniffel (game projects do not reference Shared.Models).</summary>
    public const string Type = "Kniffel";

    public override string GameType => Type;

    public override (string ActionType, string? PayloadJson) SerializeAction(KniffelAction action) => action switch
    {
        Roll roll => (Roll.ActionType, JsonSerializer.Serialize(roll, JsonOptions)),
        Score score => (Score.ActionType, JsonSerializer.Serialize(score, JsonOptions)),
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    protected override KniffelAction? ParseAction(string actionType, string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return null;
        }

        return actionType switch
        {
            Roll.ActionType => JsonSerializer.Deserialize<Roll>(payloadJson, JsonOptions),
            Score.ActionType => JsonSerializer.Deserialize<Score>(payloadJson, JsonOptions),
            _ => null,
        };
    }
}
