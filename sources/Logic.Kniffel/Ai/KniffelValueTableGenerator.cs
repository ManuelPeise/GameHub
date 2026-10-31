using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>
/// Computes <see cref="KniffelValueTable"/> by dynamic programming over all game situations (optimal solitaire
/// play with the official joker rule). Takes a few minutes; the result is shipped as embedded resource and
/// regenerated only when the rules change (explicit test in Tests.Kniffel).
/// </summary>
public static class KniffelValueTableGenerator
{
    private const int FullMask = (1 << KniffelRules.CategoryCount) - 1;

    public static float[] Generate(CancellationToken cancellationToken = default)
    {
        var values = new float[KniffelValueTable.StateCount];
        var finals = DiceTable.Finals;
        var scoreNow = new double[finals.Length];

        // Higher masks are reached later in the game, so they are computed first.
        for (var mask = FullMask - 1; mask >= 0; mask--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var kniffelFilled = (mask & (1 << (int)KniffelCategory.Kniffel)) != 0;

            for (var upper = 0; upper <= KniffelScoring.UpperBonusThreshold; upper++)
            {
                foreach (var eligible in new[] { false, true })
                {
                    if (eligible && !kniffelFilled)
                    {
                        continue;
                    }

                    for (var final = 0; final < finals.Length; final++)
                    {
                        scoreNow[final] = BestScore(values, mask, upper, eligible, final);
                    }

                    // Two more rolls are possible after the first one.
                    var afterSecond = DiceTable.BestOver(DiceTable.Expect(scoreNow));
                    var afterFirst = DiceTable.BestOver(DiceTable.Expect(afterSecond));
                    values[KniffelValueTable.Index(mask, upper, eligible)] =
                        (float)DiceTable.Expect(afterFirst)[DiceTable.EmptyKeep];
                }
            }
        }

        return values;
    }

    /// <summary>Best value of scoring a final result now: points, bonuses and the value of the next situation.</summary>
    internal static double BestScore(float[] values, int mask, int upper, bool eligible, int final)
    {
        Span<ScoreOption> options = stackalloc ScoreOption[KniffelRules.CategoryCount];
        var count = FillOptions(mask, upper, eligible, final, options);
        var best = double.MinValue;
        for (var i = 0; i < count; i++)
        {
            var option = options[i];
            var next = option.NextMask == FullMask ? 0 : values[KniffelValueTable.Index(option.NextMask, option.NextUpper, option.NextEligible)];
            best = Math.Max(best, option.Points + next);
        }

        return best;
    }

    /// <summary>
    /// All legal ways to score a final result, with the points gained (incl. bonuses) and the next situation.
    /// Returns the number of options written to <paramref name="options"/> (at most 13).
    /// </summary>
    internal static int FillOptions(int mask, int upper, bool eligible, int final, Span<ScoreOption> options)
    {
        var count = 0;
        var dice = DiceTable.Finals[final];
        var isKniffel = DiceTable.IsKniffel[final];
        var joker = isKniffel && (mask & (1 << (int)KniffelCategory.Kniffel)) != 0;
        var matchingUpper = dice[0] - 1;
        var anyLowerOpen = false;
        for (var category = 6; category < KniffelRules.CategoryCount; category++)
        {
            anyLowerOpen |= (mask & (1 << category)) == 0;
        }

        for (var index = 0; index < KniffelRules.CategoryCount; index++)
        {
            if ((mask & (1 << index)) != 0)
            {
                continue;
            }

            if (joker)
            {
                // Official joker order: matching upper box, otherwise any lower box, otherwise any upper box.
                if ((mask & (1 << matchingUpper)) == 0)
                {
                    if (index != matchingUpper)
                    {
                        continue;
                    }
                }
                else if (anyLowerOpen && index < 6)
                {
                    continue;
                }
            }

            var category = (KniffelCategory)index;
            var points = DiceTable.RawScores[final][index];
            if (joker && index >= 6)
            {
                points = category switch
                {
                    KniffelCategory.FullHouse => KniffelScoring.FullHousePoints,
                    KniffelCategory.SmallStraight => KniffelScoring.SmallStraightPoints,
                    KniffelCategory.LargeStraight => KniffelScoring.LargeStraightPoints,
                    _ => points,
                };
            }

            var gained = points;
            if (isKniffel && eligible)
            {
                gained += KniffelScoring.KniffelBonusPoints;
            }

            var nextUpper = upper;
            if (index < 6)
            {
                nextUpper = upper + points;
                if (upper < KniffelScoring.UpperBonusThreshold && nextUpper >= KniffelScoring.UpperBonusThreshold)
                {
                    gained += KniffelScoring.UpperBonus;
                }

                nextUpper = Math.Min(nextUpper, KniffelScoring.UpperBonusThreshold);
            }

            var nextEligible = eligible || (category == KniffelCategory.Kniffel && points == KniffelScoring.KniffelPoints);
            options[count++] = new ScoreOption(category, gained, mask | (1 << index), nextUpper, nextEligible);
        }

        return count;
    }
}

/// <summary>One legal way to score: category, points gained (incl. bonuses) and the resulting situation.</summary>
internal readonly record struct ScoreOption(KniffelCategory Category, int Points, int NextMask, int NextUpper, bool NextEligible);
