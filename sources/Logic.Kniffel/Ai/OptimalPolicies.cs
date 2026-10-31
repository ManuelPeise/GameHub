using Logic.Core.Abstractions;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>Situation of the own sheet as used by <see cref="KniffelValueTable"/>.</summary>
internal readonly record struct SheetSituation(int Mask, int Upper, bool Eligible)
{
    public static SheetSituation Of(KniffelSheet sheet) => new(
        KniffelValueTable.FilledMask(sheet),
        Math.Min(KniffelScoring.UpperSum(sheet), KniffelScoring.UpperBonusThreshold),
        sheet.Scores[(int)KniffelCategory.Kniffel] == KniffelScoring.KniffelPoints);
}

/// <summary>
/// Scores in the category with the highest points plus expected value of the remaining game
/// (<see cref="KniffelValueTable"/>): accounts for upper bonus, joker rule and remaining categories exactly.
/// </summary>
public sealed class OptimalCategoryPolicy : ICategoryPolicy
{
    public KniffelCategory ChooseCategory(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var situation = SheetSituation.Of(view.MySheet);
        var table = KniffelValueTable.Table;

        Span<ScoreOption> options = stackalloc ScoreOption[KniffelRules.CategoryCount];
        var count = KniffelValueTableGenerator.FillOptions(situation.Mask, situation.Upper, situation.Eligible, DiceTable.FinalOf(view.Dice), options);

        var best = options[0];
        var bestValue = double.MinValue;
        for (var i = 0; i < count; i++)
        {
            var option = options[i];
            var value = option.Points + (option.NextMask == (1 << KniffelRules.CategoryCount) - 1
                ? 0
                : table[KniffelValueTable.Index(option.NextMask, option.NextUpper, option.NextEligible)]);
            if (value > bestValue)
            {
                bestValue = value;
                best = option;
            }
        }

        return best.Category;
    }
}

/// <summary>
/// Holds the dice with the highest expected value, scoring with <see cref="OptimalCategoryPolicy"/> afterwards.
/// Hard looks ahead over all remaining rolls (optimal play); Medium only over the next roll.
/// </summary>
public sealed class OptimalHoldPolicy(bool lookAheadAllRolls) : IHoldPolicy
{
    public int? ChooseHoldMask(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var situation = SheetSituation.Of(view.MySheet);
        var table = KniffelValueTable.Table;

        var scoreNow = new double[DiceTable.Finals.Length];
        for (var final = 0; final < scoreNow.Length; final++)
        {
            scoreNow[final] = KniffelValueTableGenerator.BestScore(table, situation.Mask, situation.Upper, situation.Eligible, final);
        }

        var expected = DiceTable.Expect(scoreNow);
        if (lookAheadAllRolls && KniffelRules.MaxRolls - view.RollsUsed >= 2)
        {
            expected = DiceTable.Expect(DiceTable.BestOver(expected));
        }

        var bestMask = KniffelRules.AllHeld;
        var bestValue = expected[DiceTable.KeepOf(view.Dice, KniffelRules.AllHeld)];
        for (var mask = 0; mask < KniffelRules.AllHeld; mask++)
        {
            var candidate = expected[DiceTable.KeepOf(view.Dice, mask)];
            if (candidate > bestValue + 1e-9)
            {
                bestValue = candidate;
                bestMask = mask;
            }
        }

        return bestMask == KniffelRules.AllHeld ? null : bestMask;
    }
}
