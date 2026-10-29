using Logic.Core.Abstractions;
using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Kniffel;
using Logic.Kniffel.Domain;

namespace Tests.Kniffel;

public sealed class KniffelRulesTests
{
    private static readonly KniffelRules Rules = new();

    /// <summary>Random source that returns fixed faces (1–6) in order.</summary>
    private sealed class FixedDice(params int[] faces) : IRandom
    {
        private int _next;

        public int Next(int maxExclusive) => faces[_next++ % faces.Length] - 1;
    }

    private static KniffelState Start(int seats = 2) => Rules.CreateInitialState(seats, new SeededRandom(1));

    [Xunit.Theory]
    [Xunit.InlineData(1)]
    [Xunit.InlineData(5)]
    public void SeatCount_MustBeTwoToFour(int seats)
    {
        Xunit.Assert.Throws<ArgumentOutOfRangeException>(() => Rules.CreateInitialState(seats, new SeededRandom(1)));
    }

    [Xunit.Fact]
    public void Roll_ThrowsOnlyTheDiceThatAreNotHeld()
    {
        var state = Rules.Apply(Start(), 0, new Roll(0), new FixedDice(1, 2, 3, 4, 5));
        state = Rules.Apply(state, 0, new Roll(0b00101), new FixedDice(6, 6, 6));

        Xunit.Assert.Equal(new[] { 1, 6, 3, 6, 6 }, state.Dice.ToArray());
        Xunit.Assert.Equal(2, state.RollsUsed);
        Xunit.Assert.Equal(0b00101, state.HeldMask);
    }

    [Xunit.Fact]
    public void FourthRoll_IsRejected()
    {
        var state = Start();
        for (var roll = 0; roll < 3; roll++)
        {
            state = Rules.Apply(state, 0, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        }

        Xunit.Assert.Equal("Du hast bereits dreimal gewürfelt. Wähle eine Kategorie.", Rules.Validate(state, 0, new Roll(0)).Error);
    }

    [Xunit.Fact]
    public void HoldingBeforeTheFirstRoll_AndScoringWithoutRoll_AreRejected()
    {
        Xunit.Assert.False(Rules.Validate(Start(), 0, new Roll(1)).IsValid);
        Xunit.Assert.Equal("Bitte zuerst würfeln.", Rules.Validate(Start(), 0, new Score(KniffelCategory.Chance)).Error);
    }

    [Xunit.Fact]
    public void Score_FillsTheCategory_AndPassesTheTurn()
    {
        var state = Rules.Apply(Start(), 0, new Roll(0), new FixedDice(2, 2, 3, 3, 3));
        state = Rules.Apply(state, 0, new Score(KniffelCategory.FullHouse), new SeededRandom(1));

        Xunit.Assert.Equal(25, state.Sheets[0].Scores[(int)KniffelCategory.FullHouse]);
        Xunit.Assert.Equal(1, state.CurrentSeat);
        Xunit.Assert.Equal(0, state.RollsUsed);
        Xunit.Assert.Equal("Du bist nicht am Zug.", Rules.Validate(state, 0, new Roll(0)).Error);
    }

    [Xunit.Fact]
    public void FilledCategory_IsRejected()
    {
        var state = Rules.Apply(Start(), 0, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        state = Rules.Apply(state, 0, new Score(KniffelCategory.Chance), new SeededRandom(1));
        state = Rules.Apply(state, 1, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        state = Rules.Apply(state, 1, new Score(KniffelCategory.Ones), new SeededRandom(1));
        state = Rules.Apply(state, 0, new Roll(0), new FixedDice(1, 2, 3, 4, 6));

        Xunit.Assert.Equal("Diese Kategorie ist bereits belegt.", Rules.Validate(state, 0, new Score(KniffelCategory.Chance)).Error);
    }

    [Xunit.Fact]
    public void SecondKniffel_AddsBonus_AndJokerForcesUpperBox()
    {
        var state = Rules.Apply(Start(), 0, new Roll(0), new FixedDice(5, 5, 5, 5, 5));
        state = Rules.Apply(state, 0, new Score(KniffelCategory.Kniffel), new SeededRandom(1));
        state = Rules.Apply(state, 1, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        state = Rules.Apply(state, 1, new Score(KniffelCategory.Chance), new SeededRandom(1));
        state = Rules.Apply(state, 0, new Roll(0), new FixedDice(5, 5, 5, 5, 5));

        Xunit.Assert.False(Rules.Validate(state, 0, new Score(KniffelCategory.Chance)).IsValid);
        state = Rules.Apply(state, 0, new Score(KniffelCategory.Fives), new SeededRandom(1));

        Xunit.Assert.Equal(25, state.Sheets[0].Scores[(int)KniffelCategory.Fives]);
        Xunit.Assert.Equal(50, state.Sheets[0].KniffelBonus);
        Xunit.Assert.Equal(50 + 25 + 50, KniffelScoring.Total(state.Sheets[0]));
    }

    [Xunit.Fact]
    public void FullGame_EndsAfter13Turns_WithPlacementsAndScores()
    {
        var module = new KniffelModule();
        var random = new SeededRandom(42);
        var state = Start(3);
        while (state.Status == KniffelStatus.Running)
        {
            var seat = state.CurrentSeat;
            state = Rules.Apply(state, seat, new Roll(0), random);
            var category = KniffelScoring.LegalCategories(state.Sheets[seat], state.Dice)[0];
            state = Rules.Apply(state, seat, new Score(category), random);
        }

        Xunit.Assert.All(state.Sheets, sheet => Xunit.Assert.True(KniffelScoring.IsComplete(sheet)));
        var progress = Rules.GetProgress(state);
        Xunit.Assert.True(progress.IsFinished);
        Xunit.Assert.Equal(3, progress.Results.Count);
        Xunit.Assert.All(progress.Results, result => Xunit.Assert.Equal(KniffelScoring.Total(state.Sheets[result.Seat]), result.Score));
        Xunit.Assert.Contains(progress.Results, result => result.Placement == 1);

        // JSON round trip of a finished state.
        Xunit.Assert.Equal(module.SerializeState(state), module.SerializeState(module.DeserializeState(module.SerializeState(state))));
    }

    [Xunit.Fact]
    public void EqualTotals_ShareFirstPlace_AsDraw()
    {
        var full = new KniffelSheet([.. Enumerable.Repeat<int?>(0, KniffelRules.CategoryCount)], 0);
        var state = new KniffelState([full, full], [false, false], 0, 0, [0, 0, 0, 0, 0], 0, KniffelStatus.Finished);

        var results = Rules.GetProgress(state).Results;

        Xunit.Assert.All(results, result => Xunit.Assert.Equal(SeatOutcome.Draw, result.Outcome));
        Xunit.Assert.All(results, result => Xunit.Assert.Equal(1, result.Placement));
    }

    [Xunit.Fact]
    public void RemovedSeat_IsSkipped_AndNotScored()
    {
        var state = Start(3);
        state = Rules.Apply(state, 0, new Roll(0), new FixedDice(1, 2, 3, 4, 6));

        state = Rules.RemoveSeat(state, 0); // seat 0 was at turn

        Xunit.Assert.Equal(1, state.CurrentSeat);
        Xunit.Assert.Equal(0, state.RollsUsed);

        state = Rules.Apply(state, 1, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        state = Rules.Apply(state, 1, new Score(KniffelCategory.Chance), new SeededRandom(1));
        Xunit.Assert.Equal(2, state.CurrentSeat);
        state = Rules.Apply(state, 2, new Roll(0), new FixedDice(1, 2, 3, 4, 6));
        state = Rules.Apply(state, 2, new Score(KniffelCategory.Chance), new SeededRandom(1));
        Xunit.Assert.Equal(1, state.CurrentSeat); // seat 0 is skipped
    }

    [Xunit.Fact]
    public void Module_RejectsMalformedActions_AndAcceptsSerializedOnes()
    {
        var module = new KniffelModule();
        var json = module.CreateInitialState(2, new SeededRandom(1));

        Xunit.Assert.IsType<ActionResult.Rejected>(module.Apply(json, 0, "Score", "{\"category\":\"Nope\"}", new SeededRandom(1)));
        Xunit.Assert.IsType<ActionResult.Rejected>(module.Apply(json, 0, "Hold", "{}", new SeededRandom(1)));

        var (type, payload) = module.SerializeAction(new Roll(0));
        var applied = Xunit.Assert.IsType<ActionResult.Applied>(module.Apply(json, 0, type, payload, new SeededRandom(1)));
        Xunit.Assert.Contains("\"rollsUsed\":1", applied.StateJson, StringComparison.Ordinal);
    }
}
