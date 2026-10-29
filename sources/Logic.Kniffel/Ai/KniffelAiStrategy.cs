using Logic.Core.Abstractions;
using Logic.Core.Ai;
using Logic.Kniffel.Domain;

namespace Logic.Kniffel.Ai;

/// <summary>
/// A Kniffel AI made of two exchangeable parts: <see cref="IHoldPolicy"/> (roll again, which dice to keep)
/// and <see cref="ICategoryPolicy"/> (where to score). Decides one action at a time.
/// </summary>
public sealed class KniffelAiStrategy(string difficulty, string version, IHoldPolicy hold, ICategoryPolicy category)
    : IAiStrategy<KniffelView, KniffelAction>
{
    public static KniffelAiStrategy Easy { get; } =
        new(AiDifficulties.Easy, "kniffel-easy-1", new FrequentFaceHoldPolicy(), new HighestScoreCategoryPolicy());

    public static KniffelAiStrategy Medium { get; } =
        new(AiDifficulties.Medium, "kniffel-medium-1", ExpectedValueHoldPolicy.Medium, ValueCategoryPolicy.Medium);

    public static KniffelAiStrategy Hard { get; } =
        new(AiDifficulties.Hard, "kniffel-hard-1", ExpectedValueHoldPolicy.Hard, ValueCategoryPolicy.Hard);

    public string Difficulty => difficulty;

    public string Version => version;

    public KniffelAction ChooseAction(KniffelView view, int seat, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.RollsUsed == 0)
        {
            return new Roll(0);
        }

        if (view.RollsUsed < KniffelRules.MaxRolls && hold.ChooseHoldMask(view, random) is { } mask)
        {
            return new Roll(mask);
        }

        return new Score(category.ChooseCategory(view, random));
    }
}
