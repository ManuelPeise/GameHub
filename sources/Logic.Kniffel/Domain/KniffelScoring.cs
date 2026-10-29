namespace Logic.Kniffel.Domain;

/// <summary>Pure scoring functions of the official Kniffel rules (including the joker rule).</summary>
public static class KniffelScoring
{
    public const int UpperBonusThreshold = 63;
    public const int UpperBonus = 35;
    public const int FullHousePoints = 25;
    public const int SmallStraightPoints = 30;
    public const int LargeStraightPoints = 40;
    public const int KniffelPoints = 50;
    public const int KniffelBonusPoints = 50;

    public static bool IsUpper(KniffelCategory category) => category <= KniffelCategory.Sixes;

    /// <summary>Face value of an upper category (Ones = 1 … Sixes = 6).</summary>
    public static int Face(KniffelCategory category) => (int)category + 1;

    public static int[] Counts(IReadOnlyList<int> dice)
    {
        var counts = new int[7];
        foreach (var die in dice)
        {
            counts[die]++;
        }

        return counts;
    }

    public static bool IsKniffel(IReadOnlyList<int> dice) => dice.Count == 5 && dice[0] > 0 && dice.All(die => die == dice[0]);

    /// <summary>Points of the dice in a category by the normal rules (without joker).</summary>
    public static int RawScore(IReadOnlyList<int> dice, KniffelCategory category)
    {
        var counts = Counts(dice);
        var sum = dice.Sum();
        return category switch
        {
            <= KniffelCategory.Sixes => counts[Face(category)] * Face(category),
            KniffelCategory.ThreeOfAKind => counts.Max() >= 3 ? sum : 0,
            KniffelCategory.FourOfAKind => counts.Max() >= 4 ? sum : 0,
            KniffelCategory.FullHouse => counts.Contains(3) && counts.Contains(2) ? FullHousePoints : 0,
            KniffelCategory.SmallStraight => HasRun(counts, 4) ? SmallStraightPoints : 0,
            KniffelCategory.LargeStraight => HasRun(counts, 5) ? LargeStraightPoints : 0,
            KniffelCategory.Kniffel => counts.Max() == 5 ? KniffelPoints : 0,
            KniffelCategory.Chance => sum,
            _ => throw new ArgumentOutOfRangeException(nameof(category)),
        };
    }

    /// <summary>
    /// Joker situation: a further Kniffel while the Kniffel box is already filled (with 50 or 0).
    /// </summary>
    public static bool IsJoker(KniffelSheet sheet, IReadOnlyList<int> dice) =>
        IsKniffel(dice) && sheet.Scores[(int)KniffelCategory.Kniffel] is not null;

    /// <summary>
    /// Categories the dice may be scored in. Normally every open category; in the joker situation the
    /// official order applies: the matching upper box if open, otherwise any open lower box, otherwise any
    /// open upper box (with 0 points).
    /// </summary>
    public static IReadOnlyList<KniffelCategory> LegalCategories(KniffelSheet sheet, IReadOnlyList<int> dice)
    {
        var open = Enum.GetValues<KniffelCategory>().Where(category => sheet.Scores[(int)category] is null).ToList();
        if (!IsJoker(sheet, dice))
        {
            return open;
        }

        var matchingUpper = (KniffelCategory)(dice[0] - 1);
        if (open.Contains(matchingUpper))
        {
            return [matchingUpper];
        }

        var lower = open.Where(category => !IsUpper(category)).ToList();
        return lower.Count > 0 ? lower : open;
    }

    /// <summary>Points for scoring the dice in the category, joker rule included (without the Kniffel bonus).</summary>
    public static int Points(KniffelSheet sheet, IReadOnlyList<int> dice, KniffelCategory category)
    {
        if (!IsJoker(sheet, dice) || IsUpper(category))
        {
            return RawScore(dice, category);
        }

        // Joker in the lower section: full points for full house and straights.
        return category switch
        {
            KniffelCategory.FullHouse => FullHousePoints,
            KniffelCategory.SmallStraight => SmallStraightPoints,
            KniffelCategory.LargeStraight => LargeStraightPoints,
            _ => RawScore(dice, category),
        };
    }

    /// <summary>50 bonus points for a further Kniffel if the Kniffel box holds 50.</summary>
    public static int KniffelBonusFor(KniffelSheet sheet, IReadOnlyList<int> dice) =>
        IsKniffel(dice) && sheet.Scores[(int)KniffelCategory.Kniffel] == KniffelPoints ? KniffelBonusPoints : 0;

    public static int UpperSum(KniffelSheet sheet) =>
        Enumerable.Range(0, 6).Sum(index => sheet.Scores[index] ?? 0);

    /// <summary>35 points once the upper section reaches 63 points.</summary>
    public static int UpperBonusOf(KniffelSheet sheet) => UpperSum(sheet) >= UpperBonusThreshold ? UpperBonus : 0;

    public static int LowerSum(KniffelSheet sheet) =>
        Enumerable.Range(6, KniffelRules.CategoryCount - 6).Sum(index => sheet.Scores[index] ?? 0);

    public static int Total(KniffelSheet sheet) => UpperSum(sheet) + UpperBonusOf(sheet) + LowerSum(sheet) + sheet.KniffelBonus;

    public static bool IsComplete(KniffelSheet sheet) => sheet.Scores.All(score => score is not null);

    public static int OpenCount(KniffelSheet sheet) => sheet.Scores.Count(score => score is null);

    private static bool HasRun(int[] counts, int length)
    {
        var run = 0;
        for (var face = 1; face <= 6; face++)
        {
            run = counts[face] > 0 ? run + 1 : 0;
            if (run >= length)
            {
                return true;
            }
        }

        return false;
    }
}
