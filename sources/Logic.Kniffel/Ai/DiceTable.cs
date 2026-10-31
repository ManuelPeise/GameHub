namespace Logic.Kniffel.Ai;

/// <summary>
/// Precomputed dice combinatorics for the AI: all 252 sorted results of five dice, all 462 kept sub-sets
/// (0 to 5 dice) and, for each kept set, the probability of every final result after rolling the rest.
/// Lets the AI compute exact expected values over all hold masks in well under a millisecond.
/// </summary>
internal static class DiceTable
{
    /// <summary>Sorted final results (5 dice each).</summary>
    public static readonly int[][] Finals;

    /// <summary>Per kept set: (final index, probability) of all outcomes after rolling the remaining dice.</summary>
    public static readonly (int Final, double Probability)[][] Outcomes;

    /// <summary>Per final result: the kept-set index for each of the 32 masks of its sorted dice.</summary>
    public static readonly int[][] SubKeeps;

    /// <summary>Per final result: raw points of every category (without joker and bonus).</summary>
    public static readonly int[][] RawScores;

    /// <summary>Per final result: whether it is a Kniffel (needs the joker rules).</summary>
    public static readonly bool[] IsKniffel;

    /// <summary>Index of the empty kept set (all five dice are rolled).</summary>
    public static readonly int EmptyKeep;

    private static readonly Dictionary<long, int> FinalIndex = [];
    private static readonly Dictionary<long, int> KeepIndex = [];

    static DiceTable()
    {
        var finals = new List<int[]>();
        foreach (var dice in Multisets(5))
        {
            FinalIndex[Key(dice)] = finals.Count;
            finals.Add(dice);
        }

        Finals = [.. finals];

        var outcomes = new List<(int, double)[]>();
        for (var kept = 0; kept <= 5; kept++)
        {
            foreach (var keep in Multisets(kept))
            {
                KeepIndex[Key(keep)] = outcomes.Count;
                outcomes.Add(Distribution(keep));
            }
        }

        Outcomes = [.. outcomes];

        SubKeeps = [.. Finals.Select(dice => Enumerable.Range(0, 32).Select(mask => KeepOf(dice, mask)).ToArray())];
        RawScores = [.. Finals.Select(dice => Enum.GetValues<Domain.KniffelCategory>().Select(category => Domain.KniffelScoring.RawScore(dice, category)).ToArray())];
        IsKniffel = [.. Finals.Select(dice => Domain.KniffelScoring.IsKniffel(dice))];
        EmptyKeep = KeepIndex[Key([])];
    }

    public static int FinalOf(IReadOnlyList<int> dice) => FinalIndex[Key([.. dice.Order()])];

    /// <summary>Kept-set index for the dice selected by the mask (bit i = die i).</summary>
    public static int KeepOf(IReadOnlyList<int> dice, int mask) =>
        KeepIndex[Key([.. Enumerable.Range(0, dice.Count).Where(index => (mask & (1 << index)) != 0).Select(index => dice[index]).Order()])];

    /// <summary>Expected value of every kept set when the next result is valued with <paramref name="value"/>.</summary>
    public static double[] Expect(double[] value)
    {
        var expected = new double[Outcomes.Length];
        for (var keep = 0; keep < Outcomes.Length; keep++)
        {
            var sum = 0d;
            foreach (var (final, probability) in Outcomes[keep])
            {
                sum += probability * value[final];
            }

            expected[keep] = sum;
        }

        return expected;
    }

    /// <summary>Best value reachable from every final result with one more roll (keeping all = stopping).</summary>
    public static double[] BestOver(double[] expectedByKeep)
    {
        var best = new double[Finals.Length];
        for (var final = 0; final < Finals.Length; final++)
        {
            var max = double.MinValue;
            foreach (var keep in SubKeeps[final])
            {
                max = Math.Max(max, expectedByKeep[keep]);
            }

            best[final] = max;
        }

        return best;
    }

    private static long Key(IReadOnlyList<int> sortedDice)
    {
        // Count per face, base 6, plus the number of dice so that sets of different size never collide.
        var key = (long)sortedDice.Count;
        foreach (var die in sortedDice)
        {
            key += (long)Math.Pow(6, die);
        }

        return key;
    }

    private static (int, double)[] Distribution(int[] keep)
    {
        var rolled = 5 - keep.Length;
        var total = Math.Pow(6, rolled);
        var result = new Dictionary<int, double>();
        foreach (var roll in Sequences(rolled))
        {
            var final = FinalIndex[Key([.. keep.Concat(roll).Order()])];
            result[final] = result.GetValueOrDefault(final) + (1 / total);
        }

        return [.. result.Select(entry => (entry.Key, entry.Value))];
    }

    /// <summary>All ordered sequences of n dice (6^n, at most 7776).</summary>
    private static IEnumerable<int[]> Sequences(int n)
    {
        if (n == 0)
        {
            yield return [];
            yield break;
        }

        foreach (var rest in Sequences(n - 1))
        {
            for (var face = 1; face <= 6; face++)
            {
                yield return [.. rest, face];
            }
        }
    }

    /// <summary>All sorted multisets of n dice.</summary>
    private static IEnumerable<int[]> Multisets(int n, int minFace = 1)
    {
        if (n == 0)
        {
            yield return [];
            yield break;
        }

        for (var face = minFace; face <= 6; face++)
        {
            foreach (var rest in Multisets(n - 1, face))
            {
                yield return [face, .. rest];
            }
        }
    }
}
