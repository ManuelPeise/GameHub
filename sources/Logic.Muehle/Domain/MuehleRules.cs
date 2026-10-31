using Logic.Core.Abstractions;
using Logic.Core.Model;

namespace Logic.Muehle.Domain;

/// <summary>
/// Rules of Mühle (Nine Men's Morris) for two seats: placing, moving, flying with three stones,
/// one capture per closed mill, stones in mills are protected unless all opponent stones are in mills.
/// Draw on the third repetition of a position or after 50 moves per player without a capture.
/// </summary>
public sealed class MuehleRules : IGameRules<MuehleState, MuehleMove>
{
    public int MinSeats => 2;

    public int MaxSeats => 2;

    public MuehleState CreateInitialState(int seatCount, IRandom random)
    {
        if (seatCount != 2)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "Mühle needs exactly two seats.");
        }

        return MuehleState.Initial;
    }

    public ValidationResult Validate(MuehleState state, int seat, MuehleMove action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        if (state.Status != MuehleStatus.Running)
        {
            return ValidationResult.Invalid("Die Partie ist beendet.");
        }

        if (seat != state.NextSeat)
        {
            return ValidationResult.Invalid("Du bist nicht am Zug.");
        }

        if (!IsPoint(action.To) || (action.From is { } f && !IsPoint(f)) || (action.Remove is { } r && !IsPoint(r)))
        {
            return ValidationResult.Invalid("Diesen Punkt gibt es nicht.");
        }

        if (state.Board[action.To] != MuehleState.EmptyPoint)
        {
            return ValidationResult.Invalid("Der Zielpunkt ist besetzt.");
        }

        var color = MuehleState.ColorOf(seat);
        var phase = state.PhaseOf(seat);
        if (phase == MuehlePhase.Placing)
        {
            if (action.From is not null)
            {
                return ValidationResult.Invalid("Setze zuerst alle Steine.");
            }
        }
        else
        {
            if (action.From is not { } from)
            {
                return ValidationResult.Invalid("Du hast keine Steine mehr zum Setzen – ziehe einen Stein.");
            }

            if (state.Board[from] != color)
            {
                return ValidationResult.Invalid("Wähle einen eigenen Stein.");
            }

            if (phase == MuehlePhase.Moving && !MuehleBoard.Neighbours[from].Contains(action.To))
            {
                return ValidationResult.Invalid("Ziehe auf einen freien Nachbarpunkt.");
            }
        }

        var board = Move(state.Board, action.From, action.To, color);
        var removable = ClosesMill(board, action.To, color) ? RemovableStones(board, 1 - seat) : [];

        if (removable.Count == 0)
        {
            return action.Remove is null
                ? ValidationResult.Valid
                : ValidationResult.Invalid("Du darfst nur nach einer geschlossenen Mühle einen Stein entfernen.");
        }

        if (action.Remove is not { } remove)
        {
            return ValidationResult.Invalid("Du hast eine Mühle geschlossen – entferne einen gegnerischen Stein.");
        }

        return removable.Contains(remove)
            ? ValidationResult.Valid
            : ValidationResult.Invalid("Dieser Stein darf nicht entfernt werden (Steine in einer Mühle sind geschützt).");
    }

    public MuehleState Apply(MuehleState state, int seat, MuehleMove action, IRandom random)
    {
        var validation = Validate(state, seat, action);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        var opponent = 1 - seat;
        var cells = Move(state.Board, action.From, action.To, MuehleState.ColorOf(seat)).ToCharArray();
        if (action.Remove is { } remove)
        {
            cells[remove] = MuehleState.EmptyPoint;
        }

        var board = new string(cells);
        int[] inHand = [.. state.InHand];
        if (action.From is null)
        {
            inHand[seat]--;
        }

        var captured = action.Remove is not null;
        var bothMoving = inHand[0] == 0 && inHand[1] == 0;
        var key = MuehleState.PositionKey(board, opponent);
        var irreversible = captured || action.From is null;

        var next = state with
        {
            Board = board,
            NextSeat = opponent,
            InHand = inHand,
            LastMove = action,
            PliesWithoutCapture = captured || !bothMoving ? 0 : state.PliesWithoutCapture + 1,
            Positions = irreversible ? [key] : [.. state.Positions, key],
        };

        if (next.StonesOnBoard(opponent) + next.InHand[opponent] < 3)
        {
            return Won(next, seat, MuehleEndReason.TooFewStones);
        }

        if (!HasAnyMove(next, opponent))
        {
            return Won(next, seat, MuehleEndReason.Blocked);
        }

        if (next.Positions.Count(position => position == key) >= 3)
        {
            return next with { Status = MuehleStatus.Draw, EndReason = MuehleEndReason.Repetition };
        }

        if (next.PliesWithoutCapture >= MuehleState.NoCapturePlyLimit)
        {
            return next with { Status = MuehleStatus.Draw, EndReason = MuehleEndReason.NoCapture };
        }

        return next;
    }

    public IReadOnlyList<MuehleMove> LegalActions(MuehleState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != MuehleStatus.Running || seat != state.NextSeat)
        {
            return [];
        }

        var color = MuehleState.ColorOf(seat);
        var actions = new List<MuehleMove>();
        foreach (var (from, to) in Steps(state, seat))
        {
            var board = Move(state.Board, from, to, color);
            var removable = ClosesMill(board, to, color) ? RemovableStones(board, 1 - seat) : [];
            if (removable.Count == 0)
            {
                actions.Add(new MuehleMove(from, to, null));
            }
            else
            {
                actions.AddRange(removable.Select(remove => new MuehleMove(from, to, remove)));
            }
        }

        return actions;
    }

    public GameProgress GetProgress(MuehleState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state.Status switch
        {
            MuehleStatus.Won when state.WinnerSeat is { } winner => GameProgress.Finished(
            [
                new SeatResult(winner, SeatOutcome.Win, 1, null),
                new SeatResult(1 - winner, SeatOutcome.Loss, 2, null),
            ]),
            MuehleStatus.Draw => GameProgress.Finished(
            [
                new SeatResult(0, SeatOutcome.Draw, 1, null),
                new SeatResult(1, SeatOutcome.Draw, 1, null),
            ]),
            _ => GameProgress.Running(state.NextSeat),
        };
    }

    /// <summary>With two seats a removal always ends the game (handled by the session layer).</summary>
    public MuehleState RemoveSeat(MuehleState state, int seat) => state;

    /// <summary>True if the stone on <paramref name="point"/> is part of a closed mill of <paramref name="color"/>.</summary>
    public static bool ClosesMill(string board, int point, char color) =>
        MuehleBoard.MillsOfPoint[point].Any(mill => mill.All(p => board[p] == color));

    /// <summary>Opponent stones that may be taken: those outside mills, or all if every stone is in a mill.</summary>
    public static IReadOnlyList<int> RemovableStones(string board, int ofSeat)
    {
        var color = MuehleState.ColorOf(ofSeat);
        var stones = Enumerable.Range(0, MuehleBoard.PointCount).Where(point => board[point] == color).ToList();
        var free = stones.Where(point => !ClosesMill(board, point, color)).ToList();
        return free.Count > 0 ? free : stones;
    }

    /// <summary>All (from, to) steps of a seat without the capture (from is null while placing).</summary>
    public static IEnumerable<(int? From, int To)> Steps(MuehleState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        var color = MuehleState.ColorOf(seat);
        var empty = Enumerable.Range(0, MuehleBoard.PointCount).Where(point => state.Board[point] == MuehleState.EmptyPoint).ToList();
        switch (state.PhaseOf(seat))
        {
            case MuehlePhase.Placing:
                foreach (var to in empty)
                {
                    yield return (null, to);
                }

                break;
            case MuehlePhase.Flying:
                for (var from = 0; from < MuehleBoard.PointCount; from++)
                {
                    if (state.Board[from] != color)
                    {
                        continue;
                    }

                    foreach (var to in empty)
                    {
                        yield return (from, to);
                    }
                }

                break;
            default:
                for (var from = 0; from < MuehleBoard.PointCount; from++)
                {
                    if (state.Board[from] != color)
                    {
                        continue;
                    }

                    foreach (var to in MuehleBoard.Neighbours[from].Where(to => state.Board[to] == MuehleState.EmptyPoint))
                    {
                        yield return (from, to);
                    }
                }

                break;
        }
    }

    private static bool HasAnyMove(MuehleState state, int seat) => Steps(state, seat).Any();

    private static MuehleState Won(MuehleState state, int winner, MuehleEndReason reason) =>
        state with { Status = MuehleStatus.Won, WinnerSeat = winner, EndReason = reason };

    private static bool IsPoint(int point) => point is >= 0 and < MuehleBoard.PointCount;

    private static string Move(string board, int? from, int to, char color)
    {
        var cells = board.ToCharArray();
        if (from is { } source)
        {
            cells[source] = MuehleState.EmptyPoint;
        }

        cells[to] = color;
        return new string(cells);
    }
}
