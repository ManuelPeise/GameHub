using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>Facts of the own score sheet that the valuation needs.</summary>
public readonly record struct SheetContext(int UpperSum, int OpenCount)
{
    public static SheetContext Of(KniffelSheet sheet) => new(KniffelScoring.UpperSum(sheet), KniffelScoring.OpenCount(sheet));
}

/// <summary>Value of scoring <paramref name="points"/> in a category (higher is better).</summary>
public delegate double CategoryValue(KniffelCategory category, int points, SheetContext context);

/// <summary>Valuation of scoring in a category, shared by the category and hold policies.</summary>
public static class CategoryValues
{
    /// <summary>
    /// Rough average points a category brings when kept for later. Scoring below that value "wastes" the
    /// category (Medium and Hard subtract it; Easy crosses out the category with the lowest value).
    /// </summary>
    public static readonly double[] KeepValue = [2, 5, 8, 11, 14, 17, 17, 10, 16, 23, 20, 12, 22];

    /// <summary>Points including joker rule and Kniffel bonus.</summary>
    public static int Points(KniffelSheet sheet, IReadOnlyList<int> dice, KniffelCategory category) =>
        KniffelScoring.Points(sheet, dice, category) + KniffelScoring.KniffelBonusFor(sheet, dice);

    /// <summary>Medium: points minus the fixed value of the category.</summary>
    public static double Medium(KniffelCategory category, int points, SheetContext context) =>
        points - KeepValue[(int)category];

    /// <summary>
    /// Hard: like Medium, but the category value shrinks with the remaining turns, the upper bonus (63 points)
    /// counts, and upper scores are measured against par (three dice of the face).
    /// </summary>
    public static double Hard(KniffelCategory category, int points, SheetContext context)
    {
        var value = points - (KeepValue[(int)category] * Math.Min(1d, context.OpenCount / 10d));

        if (KniffelScoring.IsUpper(category) && context.UpperSum < KniffelScoring.UpperBonusThreshold)
        {
            value += (points - (3 * KniffelScoring.Face(category))) * 0.7;
            if (context.UpperSum + points >= KniffelScoring.UpperBonusThreshold)
            {
                value += KniffelScoring.UpperBonus;
            }
        }

        return value;
    }
}
