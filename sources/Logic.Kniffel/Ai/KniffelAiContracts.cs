using Logic.Core.Abstractions;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>
/// What the AI sees – the same as a human at the table: dice, held dice, roll number, the own score sheet,
/// the totals of the others and the open categories.
/// </summary>
public sealed record KniffelView(
    IReadOnlyList<int> Dice,
    int HeldMask,
    int RollsUsed,
    KniffelSheet MySheet,
    IReadOnlyList<int> OpponentTotals,
    int RemainingTurns,
    IReadOnlyList<KniffelCategory> LegalCategories);

/// <summary>Which dice to keep for the next roll; null = stop rolling and score now.</summary>
public interface IHoldPolicy
{
    int? ChooseHoldMask(KniffelView view, IRandom random);
}

/// <summary>Which category to score (or cross out) with the current dice.</summary>
public interface ICategoryPolicy
{
    KniffelCategory ChooseCategory(KniffelView view, IRandom random);
}
