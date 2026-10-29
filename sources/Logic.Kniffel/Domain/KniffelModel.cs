namespace Logic.Kniffel.Domain;

/// <summary>The 13 categories of the score sheet, in sheet order (upper section first).</summary>
public enum KniffelCategory
{
    Ones,
    Twos,
    Threes,
    Fours,
    Fives,
    Sixes,
    ThreeOfAKind,
    FourOfAKind,
    FullHouse,
    SmallStraight,
    LargeStraight,
    Kniffel,
    Chance,
}

public enum KniffelStatus
{
    Running,
    Finished,
}

/// <summary>
/// Score sheet of one seat. <see cref="Scores"/> has one entry per <see cref="KniffelCategory"/> (null = open);
/// <see cref="KniffelBonus"/> holds the 50-point bonuses for every further Kniffel.
/// </summary>
public sealed record KniffelSheet(IReadOnlyList<int?> Scores, int KniffelBonus)
{
    public static KniffelSheet Empty { get; } = new([.. Enumerable.Repeat<int?>(null, KniffelRules.CategoryCount)], 0);
}

/// <summary>
/// State of a Kniffel game. Dice are 1–6 after the first roll of a turn and 0 before it;
/// <see cref="HeldMask"/> shows which dice were kept for the last roll (bit i = die i).
/// </summary>
public sealed record KniffelState(
    IReadOnlyList<KniffelSheet> Sheets,
    IReadOnlyList<bool> Removed,
    int CurrentSeat,
    int RollsUsed,
    IReadOnlyList<int> Dice,
    int HeldMask,
    KniffelStatus Status);

/// <summary>Actions of a turn: roll (up to three times) and finally score a category.</summary>
public abstract record KniffelAction;

/// <summary>Rolls all dice that are not held. The first roll of a turn holds nothing.</summary>
public sealed record Roll(int HeldMask) : KniffelAction
{
    public const string ActionType = "Roll";
}

/// <summary>Scores the dice in an open category (or crosses it out with 0 points).</summary>
public sealed record Score(KniffelCategory Category) : KniffelAction
{
    public const string ActionType = "Score";
}
