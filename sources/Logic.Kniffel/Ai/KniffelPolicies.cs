using Logic.Core.Abstractions;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>Easy hold: keep the most frequent face; keep a straight from four dice on.</summary>
public sealed class FrequentFaceHoldPolicy : IHoldPolicy
{
    public int? ChooseHoldMask(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var dice = view.Dice;
        if (KniffelScoring.IsKniffel(dice))
        {
            return null;
        }

        var counts = KniffelScoring.Counts(dice);
        var straight = LongestRun(counts);
        if (straight.Count >= 4)
        {
            return straight.Count == 5 ? null : MaskOf(dice, straight);
        }

        // Most frequent face; on a tie the higher face.
        var face = Enumerable.Range(1, 6).OrderByDescending(f => counts[f]).ThenByDescending(f => f).First();
        var mask = 0;
        for (var index = 0; index < dice.Count; index++)
        {
            if (dice[index] == face)
            {
                mask |= 1 << index;
            }
        }

        return mask;
    }

    private static List<int> LongestRun(int[] counts)
    {
        var best = new List<int>();
        var current = new List<int>();
        for (var face = 1; face <= 6; face++)
        {
            if (counts[face] > 0)
            {
                current.Add(face);
                if (current.Count > best.Count)
                {
                    best = [.. current];
                }
            }
            else
            {
                current.Clear();
            }
        }

        return best;
    }

    /// <summary>Mask with one die of every given face.</summary>
    private static int MaskOf(IReadOnlyList<int> dice, List<int> faces)
    {
        var mask = 0;
        foreach (var face in faces)
        {
            for (var index = 0; index < dice.Count; index++)
            {
                if (dice[index] == face && (mask & (1 << index)) == 0)
                {
                    mask |= 1 << index;
                    break;
                }
            }
        }

        return mask;
    }
}

/// <summary>Easy category: highest immediate points; with 0 points cross out the least valuable category.</summary>
public sealed class HighestScoreCategoryPolicy : ICategoryPolicy
{
    public KniffelCategory ChooseCategory(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var best = view.LegalCategories.MaxBy(category => CategoryValues.Points(view.MySheet, view.Dice, category));
        return CategoryValues.Points(view.MySheet, view.Dice, best) > 0
            ? best
            : view.LegalCategories.MinBy(category => CategoryValues.KeepValue[(int)category]);
    }
}

/// <summary>Category choice by a value function (Medium and Hard).</summary>
public sealed class ValueCategoryPolicy(CategoryValue value) : ICategoryPolicy
{
    public static ValueCategoryPolicy Medium { get; } = new(CategoryValues.Medium);

    public static ValueCategoryPolicy Hard { get; } = new(CategoryValues.Hard);

    public KniffelCategory ChooseCategory(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var context = SheetContext.Of(view.MySheet);
        return view.LegalCategories.MaxBy(category => value(category, CategoryValues.Points(view.MySheet, view.Dice, category), context));
    }
}

/// <summary>
/// Hold by expected value: every mask is valued by the exact expectation of the following roll(s) and the
/// best category afterwards. Medium looks one roll ahead, Hard over all remaining rolls.
/// </summary>
public sealed class ExpectedValueHoldPolicy(CategoryValue value, bool lookAheadAllRolls) : IHoldPolicy
{
    public static ExpectedValueHoldPolicy Medium { get; } = new(CategoryValues.Medium, lookAheadAllRolls: false);

    public static ExpectedValueHoldPolicy Hard { get; } = new(CategoryValues.Hard, lookAheadAllRolls: true);

    public int? ChooseHoldMask(KniffelView view, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);
        var sheet = view.MySheet;

        // Value of every final result if scored right away (best legal category).
        var context = SheetContext.Of(sheet);
        var open = Enumerable.Range(0, KniffelRules.CategoryCount).Where(index => sheet.Scores[index] is null).ToArray();
        var scoreNow = new double[DiceTable.Finals.Length];
        for (var final = 0; final < scoreNow.Length; final++)
        {
            scoreNow[final] = DiceTable.IsKniffel[final]
                ? BestWithJoker(sheet, DiceTable.Finals[final], context)
                : BestOpen(open, DiceTable.RawScores[final], context);
        }

        var expected = DiceTable.Expect(scoreNow);
        var rollsLeft = KniffelRules.MaxRolls - view.RollsUsed;
        if (lookAheadAllRolls && rollsLeft >= 2)
        {
            // After the next roll one more roll is possible.
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

    private double BestOpen(int[] open, int[] rawScores, SheetContext context)
    {
        var best = double.MinValue;
        foreach (var index in open)
        {
            best = Math.Max(best, value((KniffelCategory)index, rawScores[index], context));
        }

        return best;
    }

    private double BestWithJoker(KniffelSheet sheet, int[] dice, SheetContext context) =>
        KniffelScoring.LegalCategories(sheet, dice).Max(category => value(category, CategoryValues.Points(sheet, dice, category), context));
}
