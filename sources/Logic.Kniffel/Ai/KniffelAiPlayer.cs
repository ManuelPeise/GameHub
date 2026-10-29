using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>Bridge between the session layer (JSON) and a Kniffel strategy.</summary>
public sealed class KniffelAiPlayer(KniffelModule module, IAiStrategy<KniffelView, KniffelAction> strategy) : IAiPlayer
{
    public string GameType => KniffelModule.Type;

    public string Difficulty => strategy.Difficulty;

    public string Version => strategy.Version;

    public static KniffelView ViewOf(KniffelState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);
        var sheet = state.Sheets[seat];
        return new KniffelView(
            state.Dice,
            state.HeldMask,
            state.RollsUsed,
            sheet,
            [.. Enumerable.Range(0, state.Sheets.Count)
                .Where(other => other != seat && !state.Removed[other])
                .Select(other => KniffelScoring.Total(state.Sheets[other]))],
            KniffelScoring.OpenCount(sheet),
            state.RollsUsed == 0 ? [] : KniffelScoring.LegalCategories(sheet, state.Dice));
    }

    public (string ActionType, string? PayloadJson) ChooseAction(string stateJson, int seat, IRandom random) =>
        module.SerializeAction(strategy.ChooseAction(ViewOf(module.DeserializeState(stateJson), seat), seat, random));
}
