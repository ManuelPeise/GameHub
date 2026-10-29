using System.Text.Json;
using System.Text.Json.Serialization;
using Logic.Core.Abstractions;
using Logic.Core.Model;

namespace Logic.Core.Modules;

/// <summary>
/// Base class of a game module: connects typed rules with the JSON boundary of <see cref="IGameModule"/>.
/// </summary>
public abstract class GameModule<TState, TAction>(IGameRules<TState, TAction> rules) : IGameModule
    where TState : class
    where TAction : class
{
    /// <summary>JSON format of states and actions (camelCase, enums as text).</summary>
    protected static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public abstract string GameType { get; }

    public int MinSeats => rules.MinSeats;

    public int MaxSeats => rules.MaxSeats;

    public IGameRules<TState, TAction> Rules => rules;

    public string CreateInitialState(int seatCount, IRandom random) => SerializeState(rules.CreateInitialState(seatCount, random));

    public ActionResult Apply(string stateJson, int seat, string actionType, string? payloadJson, IRandom random)
    {
        TAction? action;
        try
        {
            action = ParseAction(actionType, payloadJson);
        }
        catch (JsonException)
        {
            action = null;
        }

        if (action is null)
        {
            return new ActionResult.Rejected("Diese Aktion ist in diesem Spiel nicht möglich.");
        }

        var state = DeserializeState(stateJson);
        var validation = rules.Validate(state, seat, action);
        if (!validation.IsValid)
        {
            return new ActionResult.Rejected(validation.Error ?? "Ungültige Aktion.");
        }

        var next = rules.Apply(state, seat, action, random);
        return new ActionResult.Applied(SerializeState(next), rules.GetProgress(next));
    }

    public GameProgress GetProgress(string stateJson) => rules.GetProgress(DeserializeState(stateJson));

    public IReadOnlyList<(string ActionType, string? PayloadJson)> GetLegalActions(string stateJson, int seat) =>
    [
        .. rules.LegalActions(DeserializeState(stateJson), seat).Select(SerializeAction),
    ];

    public TState DeserializeState(string stateJson) =>
        JsonSerializer.Deserialize<TState>(stateJson, JsonOptions)
        ?? throw new InvalidOperationException($"{GameType}: empty game state.");

    public string SerializeState(TState state) => JsonSerializer.Serialize(state, JsonOptions);

    /// <summary>Action type and payload JSON of an action (e.g. for AI moves).</summary>
    public abstract (string ActionType, string? PayloadJson) SerializeAction(TAction action);

    /// <summary>Creates the typed action; null if the type is unknown for this game.</summary>
    protected abstract TAction? ParseAction(string actionType, string? payloadJson);
}
