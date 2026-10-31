using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.Muehle;
using Logic.Muehle.Domain;

namespace Tests.Muehle;

public sealed class MuehleRulesTests
{
    private static readonly MuehleRules Rules = new();
    private static readonly SeededRandom Random = new(1);

    /// <summary>A position after placing: white and black stones on the given points, white to move.</summary>
    private static MuehleState Position(int[] white, int[] black, int nextSeat = 0, int whiteInHand = 0, int blackInHand = 0)
    {
        var cells = new string(MuehleState.EmptyPoint, MuehleBoard.PointCount).ToCharArray();
        foreach (var point in white)
        {
            cells[point] = 'W';
        }

        foreach (var point in black)
        {
            cells[point] = 'B';
        }

        return MuehleState.Initial with { Board = new string(cells), NextSeat = nextSeat, InHand = [whiteInHand, blackInHand] };
    }

    private static MuehleState Play(MuehleState state, params MuehleMove[] moves)
    {
        foreach (var move in moves)
        {
            state = Rules.Apply(state, state.NextSeat, move, Random);
        }

        return state;
    }

    private static MuehleMove Place(int to, int? remove = null) => new(null, to, remove);

    private static MuehleMove Step(int from, int to, int? remove = null) => new(from, to, remove);

    [Xunit.Fact]
    public void Board_Has16Mills_EveryPointInTwoMills_AndSymmetricNeighbours()
    {
        Xunit.Assert.Equal(16, MuehleBoard.Mills.Count);
        Xunit.Assert.All(MuehleBoard.MillsOfPoint, mills => Xunit.Assert.Equal(2, mills.Count));
        for (var point = 0; point < MuehleBoard.PointCount; point++)
        {
            foreach (var neighbour in MuehleBoard.Neighbours[point])
            {
                Xunit.Assert.Contains(point, MuehleBoard.Neighbours[neighbour]);
            }
        }

        Xunit.Assert.Equal(64, MuehleBoard.Neighbours.Sum(list => list.Count)); // 32 lines
        Xunit.Assert.Equal(new[] { 1, 7 }, MuehleBoard.Neighbours[0]);
        Xunit.Assert.Equal(new[] { 1, 8, 10, 17 }, MuehleBoard.Neighbours[9]);
    }

    [Xunit.Fact]
    public void InitialState_EmptyBoard_NineStonesEach_WhiteStarts()
    {
        var state = Rules.CreateInitialState(2, Random);

        Xunit.Assert.Equal(new string('-', 24), state.Board);
        Xunit.Assert.Equal(new[] { 9, 9 }, state.InHand);
        Xunit.Assert.Equal(0, state.NextSeat);
        Xunit.Assert.Equal(MuehlePhase.Placing, state.PhaseOf(0));
        Xunit.Assert.Throws<ArgumentOutOfRangeException>(() => Rules.CreateInitialState(3, Random));
    }

    [Xunit.Fact]
    public void Placing_AlternatesSeats_AndReducesStonesInHand()
    {
        var state = Play(MuehleState.Initial, Place(0), Place(5));

        Xunit.Assert.Equal('W', state.Board[0]);
        Xunit.Assert.Equal('B', state.Board[5]);
        Xunit.Assert.Equal(new[] { 8, 8 }, state.InHand);
        Xunit.Assert.Equal(0, state.NextSeat);
        Xunit.Assert.Equal(new MuehleMove(null, 5, null), state.LastMove);
    }

    [Xunit.Fact]
    public void Invalid_Actions_AreRejected()
    {
        var state = Play(MuehleState.Initial, Place(0));

        Xunit.Assert.False(Rules.Validate(state, 0, Place(1)).IsValid); // not my turn
        Xunit.Assert.False(Rules.Validate(state, 1, Place(0)).IsValid); // occupied
        Xunit.Assert.False(Rules.Validate(state, 1, Place(24)).IsValid); // no such point
        Xunit.Assert.False(Rules.Validate(state, 1, Step(0, 1)).IsValid); // moving while placing
        Xunit.Assert.False(Rules.Validate(state, 1, Place(1, 0)).IsValid); // removal without mill
        Xunit.Assert.Throws<InvalidOperationException>(() => Rules.Apply(state, 1, Place(0), Random));
    }

    [Xunit.Fact]
    public void ClosingAMill_RequiresRemovingAnOpponentStone()
    {
        var state = Position([0, 1], [10, 12], whiteInHand: 7, blackInHand: 7);

        Xunit.Assert.False(Rules.Validate(state, 0, Place(2)).IsValid);
        Xunit.Assert.False(Rules.Validate(state, 0, Place(2, 0)).IsValid); // own stone
        Xunit.Assert.False(Rules.Validate(state, 0, Place(2, 5)).IsValid); // empty point

        var next = Rules.Apply(state, 0, Place(2, 10), Random);

        Xunit.Assert.Equal('-', next.Board[10]);
        Xunit.Assert.Equal(1, next.StonesOnBoard(1));
        Xunit.Assert.Equal(MuehleStatus.Running, next.Status);
    }

    [Xunit.Fact]
    public void StonesInAMill_AreProtected_UnlessAllStonesAreInMills()
    {
        var protectedState = Position([0, 1, 5], [8, 9, 10, 20], whiteInHand: 5, blackInHand: 5);

        Xunit.Assert.False(Rules.Validate(protectedState, 0, Place(2, 9)).IsValid);
        Xunit.Assert.True(Rules.Validate(protectedState, 0, Place(2, 20)).IsValid);

        var allInMills = Position([0, 1, 5], [8, 9, 10], whiteInHand: 5, blackInHand: 6);

        Xunit.Assert.True(Rules.Validate(allInMills, 0, Place(2, 9)).IsValid);
    }

    [Xunit.Fact]
    public void ClosingTwoMills_RemovesOnlyOneStone()
    {
        // Point 2 closes 0-1-2 and 2-3-4 at once.
        var state = Position([0, 1, 3, 4], [8, 12, 14, 20, 22], whiteInHand: 1, blackInHand: 0);
        var next = Rules.Apply(state, 0, Place(2, 8), Random);

        Xunit.Assert.Equal(4, next.StonesOnBoard(1));
    }

    [Xunit.Fact]
    public void Moving_OnlyToFreeNeighbours_WithOwnStones()
    {
        var state = Position([0, 4, 13, 22], [2, 6, 11, 18]);

        Xunit.Assert.Equal(MuehlePhase.Moving, state.PhaseOf(0));
        Xunit.Assert.True(Rules.Validate(state, 0, Step(0, 1)).IsValid);
        Xunit.Assert.False(Rules.Validate(state, 0, Step(0, 3)).IsValid); // not a neighbour
        Xunit.Assert.False(Rules.Validate(state, 0, Step(2, 1)).IsValid); // opponent stone
        Xunit.Assert.False(Rules.Validate(state, 0, Step(5, 3)).IsValid); // empty point
        Xunit.Assert.False(Rules.Validate(state, 0, Place(1)).IsValid); // nothing left to place
    }

    [Xunit.Fact]
    public void WithThreeStones_APlayerMayFly()
    {
        var state = Position([0, 4, 13], [2, 6, 11, 18]);

        Xunit.Assert.Equal(MuehlePhase.Flying, state.PhaseOf(0));
        Xunit.Assert.True(Rules.Validate(state, 0, Step(0, 20)).IsValid);
        Xunit.Assert.Equal(3 * 17, Rules.LegalActions(state, 0).Count);
    }

    [Xunit.Fact]
    public void FewerThanThreeStones_Loses()
    {
        var state = Position([0, 1, 3], [20, 21, 22]);

        var next = Rules.Apply(state, 0, Step(3, 2, 20), Random);

        Xunit.Assert.Equal(MuehleStatus.Won, next.Status);
        Xunit.Assert.Equal(0, next.WinnerSeat);
        Xunit.Assert.Equal(MuehleEndReason.TooFewStones, next.EndReason);
        var progress = Rules.GetProgress(next);
        Xunit.Assert.True(progress.IsFinished);
        Xunit.Assert.Contains(new SeatResult(0, SeatOutcome.Win, 1, null), progress.Results);
        Xunit.Assert.Contains(new SeatResult(1, SeatOutcome.Loss, 2, null), progress.Results);
    }

    [Xunit.Fact]
    public void APlayerWhoCannotMove_Loses()
    {
        var state = Position([2, 6, 8, 10, 15, 16], [0, 1, 7, 9]);

        var next = Rules.Apply(state, 0, Step(16, 17), Random);

        Xunit.Assert.Equal(MuehleStatus.Won, next.Status);
        Xunit.Assert.Equal(0, next.WinnerSeat);
        Xunit.Assert.Equal(MuehleEndReason.Blocked, next.EndReason);
        Xunit.Assert.Empty(Rules.LegalActions(next, 1));
    }

    [Xunit.Fact]
    public void ThirdRepetitionOfAPosition_IsADraw()
    {
        var state = Position([0, 2, 4, 6], [16, 18, 20, 22]);
        MuehleMove[] cycle = [Step(0, 7), Step(16, 23), Step(7, 0), Step(23, 16)];

        state = Play(state, [.. cycle, .. cycle]);
        Xunit.Assert.Equal(MuehleStatus.Running, state.Status);

        state = Play(state, cycle[0]);
        Xunit.Assert.Equal(MuehleStatus.Draw, state.Status);
        Xunit.Assert.Equal(MuehleEndReason.Repetition, state.EndReason);
        Xunit.Assert.All(Rules.GetProgress(state).Results, result => Xunit.Assert.Equal(SeatOutcome.Draw, result.Outcome));
    }

    [Xunit.Fact]
    public void FiftyMovesEachWithoutCapture_IsADraw()
    {
        var state = Position([0, 2, 4, 6], [16, 18, 20, 22]) with { PliesWithoutCapture = MuehleState.NoCapturePlyLimit - 1 };

        var next = Rules.Apply(state, 0, Step(0, 1), Random);

        Xunit.Assert.Equal(MuehleStatus.Draw, next.Status);
        Xunit.Assert.Equal(MuehleEndReason.NoCapture, next.EndReason);
    }

    [Xunit.Fact]
    public void Capture_ResetsTheNoCaptureCounter_AndPlacingDoesNotCount()
    {
        var placing = Play(MuehleState.Initial, Place(0), Place(5));
        Xunit.Assert.Equal(0, placing.PliesWithoutCapture);

        var state = Position([0, 1, 3, 12], [20, 21, 22, 14]) with { PliesWithoutCapture = 40 };
        Xunit.Assert.Equal(41, Rules.Apply(state, 0, Step(12, 11), Random).PliesWithoutCapture);
        Xunit.Assert.Equal(0, Rules.Apply(state, 0, Step(3, 2, 14), Random).PliesWithoutCapture);
    }

    [Xunit.Fact]
    public void RandomGames_UseOnlyLegalActions_AndAlwaysEnd()
    {
        var random = new System.Random(7);
        for (var game = 0; game < 200; game++)
        {
            var state = MuehleState.Initial;
            var plies = 0;
            while (state.Status == MuehleStatus.Running)
            {
                var actions = Rules.LegalActions(state, state.NextSeat);
                Xunit.Assert.NotEmpty(actions);
                Xunit.Assert.Empty(Rules.LegalActions(state, 1 - state.NextSeat));
                var action = actions[random.Next(actions.Count)];
                Xunit.Assert.True(Rules.Validate(state, state.NextSeat, action).IsValid);
                state = Rules.Apply(state, state.NextSeat, action, Random);
                Xunit.Assert.True(++plies < 2000);
            }

            Xunit.Assert.True(Rules.GetProgress(state).IsFinished);
        }
    }

    [Xunit.Fact]
    public void Module_SerializesStateAndActions()
    {
        var module = new MuehleModule();
        var stateJson = module.CreateInitialState(2, Random);
        var (actionType, payload) = module.SerializeAction(Place(3));

        var result = module.Apply(stateJson, 0, actionType, payload, Random);

        var applied = Xunit.Assert.IsType<ActionResult.Applied>(result);
        Xunit.Assert.Equal('W', module.DeserializeState(applied.StateJson).Board[3]);
        Xunit.Assert.Equal(1, applied.Progress.CurrentSeat);
        Xunit.Assert.Equal("MuehleMove", actionType);
        Xunit.Assert.Contains("\"to\":3", payload, StringComparison.Ordinal);
        Xunit.Assert.IsType<ActionResult.Rejected>(module.Apply(applied.StateJson, 1, actionType, payload, Random));
        Xunit.Assert.Equal(24, module.GetLegalActions(stateJson, 0).Count);
    }
}
