using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.TicTacToe.Domain;

namespace Tests.TicTacToe;

public sealed class TicTacToeRulesTests
{
    private static readonly TicTacToeRules Rules = new();
    private static readonly SeededRandom Random = new(1);

    /// <summary>Plays the given cells alternately, starting with seat 0 (X).</summary>
    private static TicTacToeState Play(params int[] cells)
    {
        var state = Rules.CreateInitialState(2, Random);
        foreach (var cell in cells)
        {
            state = Rules.Apply(state, state.NextSeat, new PlaceMark(cell), Random);
        }

        return state;
    }

    [Xunit.Fact]
    public void InitialState_IsEmpty_AndSeat0Starts()
    {
        var state = Rules.CreateInitialState(2, Random);

        Xunit.Assert.Equal("---------", state.Board);
        Xunit.Assert.Equal(0, state.NextSeat);
        Xunit.Assert.Equal(TicTacToeStatus.Running, state.Status);
    }

    [Xunit.Theory]
    [Xunit.InlineData(1)]
    [Xunit.InlineData(3)]
    public void InitialState_RequiresTwoSeats(int seats)
    {
        Xunit.Assert.Throws<ArgumentOutOfRangeException>(() => Rules.CreateInitialState(seats, Random));
    }

    [Xunit.Fact]
    public void Apply_PlacesSymbolAndSwitchesTurn()
    {
        var state = Play(4);

        Xunit.Assert.Equal("----X----", state.Board);
        Xunit.Assert.Equal(1, state.NextSeat);

        state = Rules.Apply(state, 1, new PlaceMark(0), Random);
        Xunit.Assert.Equal("O---X----", state.Board);
        Xunit.Assert.Equal(0, state.NextSeat);
    }

    public static Xunit.TheoryData<int[]> AllLines() => [.. TicTacToeRules.Lines];

    [Xunit.Theory]
    [Xunit.MemberData(nameof(AllLines))]
    public void EveryLine_WinsForSeat0(int[] line)
    {
        // X plays the line, O plays two other cells that never complete a line with X's moves.
        var others = Enumerable.Range(0, 9).Except(line).ToArray();
        var state = Rules.CreateInitialState(2, Random);
        for (var i = 0; i < 3; i++)
        {
            state = Rules.Apply(state, 0, new PlaceMark(line[i]), Random);
            if (i < 2)
            {
                var oCell = others.First(cell => state.Board[cell] == TicTacToeState.EmptyCell
                    && !WouldWin(state.Board, cell, 'O'));
                state = Rules.Apply(state, 1, new PlaceMark(oCell), Random);
            }
        }

        Xunit.Assert.Equal(TicTacToeStatus.Won, state.Status);
        Xunit.Assert.Equal(0, state.WinnerSeat);
        Xunit.Assert.Equal(line, state.WinningLine!.ToArray());

        var progress = Rules.GetProgress(state);
        Xunit.Assert.True(progress.IsFinished);
        Xunit.Assert.Contains(new SeatResult(0, SeatOutcome.Win, 1, null), progress.Results);
        Xunit.Assert.Contains(new SeatResult(1, SeatOutcome.Loss, 2, null), progress.Results);
    }

    [Xunit.Fact]
    public void Seat1_CanWin()
    {
        // X: 0, 1, 8   O: 3, 4, 5 (middle row)
        var state = Play(0, 3, 1, 4, 8, 5);

        Xunit.Assert.Equal(TicTacToeStatus.Won, state.Status);
        Xunit.Assert.Equal(1, state.WinnerSeat);
    }

    [Xunit.Fact]
    public void FullBoardWithoutLine_IsDraw()
    {
        // X O X / X O O / O X X
        var state = Play(0, 1, 2, 4, 3, 5, 7, 6, 8);

        Xunit.Assert.Equal(TicTacToeStatus.Draw, state.Status);
        var progress = Rules.GetProgress(state);
        Xunit.Assert.True(progress.IsFinished);
        Xunit.Assert.All(progress.Results, result => Xunit.Assert.Equal(SeatOutcome.Draw, result.Outcome));
    }

    [Xunit.Fact]
    public void OccupiedCell_IsRejected()
    {
        var state = Play(4);

        var validation = Rules.Validate(state, 1, new PlaceMark(4));

        Xunit.Assert.False(validation.IsValid);
        Xunit.Assert.Equal("Das Feld ist bereits belegt.", validation.Error);
    }

    [Xunit.Fact]
    public void MoveOutOfTurn_IsRejected()
    {
        var validation = Rules.Validate(Play(4), 0, new PlaceMark(0));

        Xunit.Assert.Equal("Du bist nicht am Zug.", validation.Error);
    }

    [Xunit.Theory]
    [Xunit.InlineData(-1)]
    [Xunit.InlineData(9)]
    public void CellOutsideBoard_IsRejected(int cell)
    {
        var validation = Rules.Validate(Play(), 0, new PlaceMark(cell));

        Xunit.Assert.Equal("Dieses Feld gibt es nicht.", validation.Error);
    }

    [Xunit.Fact]
    public void MoveAfterEnd_IsRejected()
    {
        var state = Play(0, 3, 1, 4, 2); // X wins with the top row

        var validation = Rules.Validate(state, 1, new PlaceMark(8));

        Xunit.Assert.Equal("Die Partie ist beendet.", validation.Error);
        Xunit.Assert.Empty(Rules.LegalActions(state, 1));
    }

    [Xunit.Fact]
    public void Apply_WithInvalidAction_Throws()
    {
        Xunit.Assert.Throws<InvalidOperationException>(() => Rules.Apply(Play(4), 1, new PlaceMark(4), Random));
    }

    [Xunit.Fact]
    public void LegalActions_AreTheEmptyCells_OnlyForTheSeatToMove()
    {
        var state = Play(4, 0);

        Xunit.Assert.Equal(new[] { 1, 2, 3, 5, 6, 7, 8 }, Rules.LegalActions(state, 0).Select(action => action.Cell));
        Xunit.Assert.Empty(Rules.LegalActions(state, 1));
    }

    [Xunit.Fact]
    public void Running_ProgressNamesTheSeatToMove()
    {
        var progress = Rules.GetProgress(Play(4));

        Xunit.Assert.False(progress.IsFinished);
        Xunit.Assert.Equal(1, progress.CurrentSeat);
    }

    private static bool WouldWin(string board, int cell, char symbol)
    {
        var cells = board.ToCharArray();
        cells[cell] = symbol;
        return TicTacToeRules.Lines.Any(line => line.All(index => cells[index] == symbol));
    }
}
