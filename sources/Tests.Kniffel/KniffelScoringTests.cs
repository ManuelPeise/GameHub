using Logic.Kniffel.Domain;

namespace Tests.Kniffel;

public sealed class KniffelScoringTests
{
    private static int[] D(string dice) => [.. dice.Select(c => c - '0')];

    private static KniffelSheet Sheet(params (KniffelCategory Category, int Points)[] filled)
    {
        var scores = KniffelSheet.Empty.Scores.ToArray();
        foreach (var (category, points) in filled)
        {
            scores[(int)category] = points;
        }

        return new KniffelSheet(scores, 0);
    }

    [Xunit.Theory]
    [Xunit.InlineData("11236", KniffelCategory.Ones, 2)]
    [Xunit.InlineData("22223", KniffelCategory.Twos, 8)]
    [Xunit.InlineData("33345", KniffelCategory.Threes, 9)]
    [Xunit.InlineData("12356", KniffelCategory.Fours, 0)]
    [Xunit.InlineData("55551", KniffelCategory.Fives, 20)]
    [Xunit.InlineData("66666", KniffelCategory.Sixes, 30)]
    [Xunit.InlineData("33345", KniffelCategory.ThreeOfAKind, 18)]
    [Xunit.InlineData("33445", KniffelCategory.ThreeOfAKind, 0)]
    [Xunit.InlineData("44446", KniffelCategory.FourOfAKind, 22)]
    [Xunit.InlineData("44466", KniffelCategory.FourOfAKind, 0)]
    [Xunit.InlineData("66666", KniffelCategory.FourOfAKind, 30)]
    [Xunit.InlineData("22333", KniffelCategory.FullHouse, 25)]
    [Xunit.InlineData("22223", KniffelCategory.FullHouse, 0)]
    [Xunit.InlineData("55555", KniffelCategory.FullHouse, 0)]
    [Xunit.InlineData("12346", KniffelCategory.SmallStraight, 30)]
    [Xunit.InlineData("34561", KniffelCategory.SmallStraight, 30)]
    [Xunit.InlineData("23345", KniffelCategory.SmallStraight, 30)]
    [Xunit.InlineData("12456", KniffelCategory.SmallStraight, 0)]
    [Xunit.InlineData("12345", KniffelCategory.SmallStraight, 30)]
    [Xunit.InlineData("12345", KniffelCategory.LargeStraight, 40)]
    [Xunit.InlineData("23456", KniffelCategory.LargeStraight, 40)]
    [Xunit.InlineData("12346", KniffelCategory.LargeStraight, 0)]
    [Xunit.InlineData("44444", KniffelCategory.Kniffel, 50)]
    [Xunit.InlineData("44443", KniffelCategory.Kniffel, 0)]
    [Xunit.InlineData("12345", KniffelCategory.Chance, 15)]
    [Xunit.InlineData("66665", KniffelCategory.Chance, 29)]
    public void AllCategories_AreScoredCorrectly(string dice, KniffelCategory category, int expected)
    {
        Xunit.Assert.Equal(expected, KniffelScoring.Points(KniffelSheet.Empty, D(dice), category));
    }

    [Xunit.Fact]
    public void UpperBonus_StartsAtExactly63()
    {
        // 3 × each face = 63
        var exact = Sheet((KniffelCategory.Ones, 3), (KniffelCategory.Twos, 6), (KniffelCategory.Threes, 9),
            (KniffelCategory.Fours, 12), (KniffelCategory.Fives, 15), (KniffelCategory.Sixes, 18));
        var below = Sheet((KniffelCategory.Ones, 2), (KniffelCategory.Twos, 6), (KniffelCategory.Threes, 9),
            (KniffelCategory.Fours, 12), (KniffelCategory.Fives, 15), (KniffelCategory.Sixes, 18));

        Xunit.Assert.Equal(35, KniffelScoring.UpperBonusOf(exact));
        Xunit.Assert.Equal(63 + 35, KniffelScoring.Total(exact));
        Xunit.Assert.Equal(0, KniffelScoring.UpperBonusOf(below));
    }

    [Xunit.Fact]
    public void FurtherKniffel_Gives50Bonus_OnlyIfKniffelBoxHas50()
    {
        Xunit.Assert.Equal(50, KniffelScoring.KniffelBonusFor(Sheet((KniffelCategory.Kniffel, 50)), D("33333")));
        Xunit.Assert.Equal(0, KniffelScoring.KniffelBonusFor(Sheet((KniffelCategory.Kniffel, 0)), D("33333")));
        Xunit.Assert.Equal(0, KniffelScoring.KniffelBonusFor(Sheet(), D("33333")));
    }

    [Xunit.Fact]
    public void Joker_MustUseMatchingUpperBox_IfOpen()
    {
        var legal = KniffelScoring.LegalCategories(Sheet((KniffelCategory.Kniffel, 50)), D("44444"));

        Xunit.Assert.Equal(new[] { KniffelCategory.Fours }, legal.ToArray());
    }

    [Xunit.Fact]
    public void Joker_InLowerSection_GivesFullPointsForFullHouseAndStraights()
    {
        var sheet = Sheet((KniffelCategory.Kniffel, 50), (KniffelCategory.Fours, 16));
        var dice = D("44444");

        var legal = KniffelScoring.LegalCategories(sheet, dice);
        Xunit.Assert.DoesNotContain(KniffelCategory.Ones, legal);
        Xunit.Assert.Equal(25, KniffelScoring.Points(sheet, dice, KniffelCategory.FullHouse));
        Xunit.Assert.Equal(30, KniffelScoring.Points(sheet, dice, KniffelCategory.SmallStraight));
        Xunit.Assert.Equal(40, KniffelScoring.Points(sheet, dice, KniffelCategory.LargeStraight));
        Xunit.Assert.Equal(20, KniffelScoring.Points(sheet, dice, KniffelCategory.Chance));
    }

    [Xunit.Fact]
    public void Joker_WithLowerSectionFull_CrossesOutAnUpperBox()
    {
        var sheet = Sheet((KniffelCategory.Kniffel, 0), (KniffelCategory.Fours, 12), (KniffelCategory.ThreeOfAKind, 20),
            (KniffelCategory.FourOfAKind, 20), (KniffelCategory.FullHouse, 25), (KniffelCategory.SmallStraight, 30),
            (KniffelCategory.LargeStraight, 40), (KniffelCategory.Chance, 20));

        var legal = KniffelScoring.LegalCategories(sheet, D("44444"));

        Xunit.Assert.Equal(new[] { KniffelCategory.Ones, KniffelCategory.Twos, KniffelCategory.Threes, KniffelCategory.Fives, KniffelCategory.Sixes }, legal.ToArray());
        Xunit.Assert.Equal(0, KniffelScoring.Points(sheet, D("44444"), KniffelCategory.Ones));
    }

    [Xunit.Fact]
    public void JokerAfterCrossedOutKniffel_StillApplies_ButWithoutBonus()
    {
        var sheet = Sheet((KniffelCategory.Kniffel, 0), (KniffelCategory.Twos, 4));

        Xunit.Assert.True(KniffelScoring.IsJoker(sheet, D("22222")));
        Xunit.Assert.Equal(25, KniffelScoring.Points(sheet, D("22222"), KniffelCategory.FullHouse));
        Xunit.Assert.Equal(0, KniffelScoring.KniffelBonusFor(sheet, D("22222")));
    }
}
