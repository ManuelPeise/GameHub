using Logic.Core.Abstractions;
using Logic.Core.Model;

namespace Logic.Kniffel.Domain;

/// <summary>Kniffel for 2 to 4 seats: 13 turns per seat, up to three rolls per turn, official joker rule.</summary>
public sealed class KniffelRules : IGameRules<KniffelState, KniffelAction>
{
    public const int CategoryCount = 13;
    public const int DiceCount = 5;
    public const int MaxRolls = 3;
    public const int AllHeld = (1 << DiceCount) - 1;

    private static readonly int[] NoDice = [0, 0, 0, 0, 0];

    public int MinSeats => 2;

    public int MaxSeats => 4;

    public KniffelState CreateInitialState(int seatCount, IRandom random)
    {
        if (seatCount < MinSeats || seatCount > MaxSeats)
        {
            throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "Kniffel needs 2 to 4 seats.");
        }

        return new KniffelState(
            [.. Enumerable.Repeat(KniffelSheet.Empty, seatCount)],
            [.. Enumerable.Repeat(false, seatCount)],
            0,
            0,
            NoDice,
            0,
            KniffelStatus.Running);
    }

    public ValidationResult Validate(KniffelState state, int seat, KniffelAction action)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);

        if (state.Status != KniffelStatus.Running)
        {
            return ValidationResult.Invalid("Die Partie ist beendet.");
        }

        if (seat != state.CurrentSeat)
        {
            return ValidationResult.Invalid("Du bist nicht am Zug.");
        }

        switch (action)
        {
            case Roll roll:
                if (state.RollsUsed >= MaxRolls)
                {
                    return ValidationResult.Invalid("Du hast bereits dreimal gewürfelt. Wähle eine Kategorie.");
                }

                if (roll.HeldMask is < 0 or > AllHeld)
                {
                    return ValidationResult.Invalid("Diese Würfelauswahl ist ungültig.");
                }

                if (state.RollsUsed == 0 && roll.HeldMask != 0)
                {
                    return ValidationResult.Invalid("Vor dem ersten Wurf kannst du keine Würfel halten.");
                }

                if (roll.HeldMask == AllHeld)
                {
                    return ValidationResult.Invalid("Halte nicht alle Würfel, sonst ändert sich nichts.");
                }

                return ValidationResult.Valid;

            case Score score:
                if (state.RollsUsed == 0)
                {
                    return ValidationResult.Invalid("Bitte zuerst würfeln.");
                }

                var sheet = state.Sheets[seat];
                if (sheet.Scores[(int)score.Category] is not null)
                {
                    return ValidationResult.Invalid("Diese Kategorie ist bereits belegt.");
                }

                if (!KniffelScoring.LegalCategories(sheet, state.Dice).Contains(score.Category))
                {
                    return ValidationResult.Invalid("Joker-Regel: Dieser Kniffel muss in einer anderen Kategorie gewertet werden.");
                }

                return ValidationResult.Valid;

            default:
                return ValidationResult.Invalid("Diese Aktion ist in diesem Spiel nicht möglich.");
        }
    }

    public KniffelState Apply(KniffelState state, int seat, KniffelAction action, IRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var validation = Validate(state, seat, action);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(validation.Error);
        }

        return action switch
        {
            Roll roll => ApplyRoll(state, roll, random),
            Score score => ApplyScore(state, seat, score),
            _ => throw new InvalidOperationException("Unknown action."),
        };
    }

    public IReadOnlyList<KniffelAction> LegalActions(KniffelState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status != KniffelStatus.Running || seat != state.CurrentSeat)
        {
            return [];
        }

        if (state.RollsUsed == 0)
        {
            return [new Roll(0)];
        }

        var actions = new List<KniffelAction>();
        if (state.RollsUsed < MaxRolls)
        {
            actions.AddRange(Enumerable.Range(0, AllHeld).Select(mask => new Roll(mask)));
        }

        actions.AddRange(KniffelScoring.LegalCategories(state.Sheets[seat], state.Dice).Select(category => new Score(category)));
        return actions;
    }

    public GameProgress GetProgress(KniffelState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Status == KniffelStatus.Running)
        {
            return GameProgress.Running(state.CurrentSeat);
        }

        // Placement by total among the remaining seats; equal totals share the placement.
        var totals = Enumerable.Range(0, state.Sheets.Count)
            .Where(seat => !state.Removed[seat])
            .Select(seat => (Seat: seat, Total: KniffelScoring.Total(state.Sheets[seat])))
            .ToList();
        var best = totals.Max(entry => entry.Total);
        var winners = totals.Count(entry => entry.Total == best);

        return GameProgress.Finished(
        [
            .. totals.Select(entry =>
            {
                var placement = 1 + totals.Count(other => other.Total > entry.Total);
                var outcome = entry.Total < best ? SeatOutcome.Loss : winners > 1 ? SeatOutcome.Draw : SeatOutcome.Win;
                return new SeatResult(entry.Seat, outcome, placement, entry.Total);
            }),
        ]);
    }

    /// <summary>The removed seat is skipped; if it was at turn, the next seat starts a fresh turn.</summary>
    public KniffelState RemoveSeat(KniffelState state, int seat)
    {
        ArgumentNullException.ThrowIfNull(state);

        var removed = state.Removed.ToArray();
        removed[seat] = true;
        var next = state with { Removed = removed };
        if (state.Status != KniffelStatus.Running)
        {
            return next;
        }

        if (AllActiveComplete(next))
        {
            return next with { Status = KniffelStatus.Finished, RollsUsed = 0, Dice = NoDice, HeldMask = 0 };
        }

        return state.CurrentSeat == seat
            ? next with { CurrentSeat = NextSeat(next, seat), RollsUsed = 0, Dice = NoDice, HeldMask = 0 }
            : next;
    }

    private static KniffelState ApplyRoll(KniffelState state, Roll roll, IRandom random)
    {
        var dice = state.Dice.ToArray();
        for (var index = 0; index < DiceCount; index++)
        {
            if ((roll.HeldMask & (1 << index)) == 0)
            {
                dice[index] = random.Next(6) + 1;
            }
        }

        return state with { Dice = dice, RollsUsed = state.RollsUsed + 1, HeldMask = roll.HeldMask };
    }

    private static KniffelState ApplyScore(KniffelState state, int seat, Score score)
    {
        var sheet = state.Sheets[seat];
        var scores = sheet.Scores.ToArray();
        scores[(int)score.Category] = KniffelScoring.Points(sheet, state.Dice, score.Category);
        var updated = new KniffelSheet(scores, sheet.KniffelBonus + KniffelScoring.KniffelBonusFor(sheet, state.Dice));

        var sheets = state.Sheets.ToArray();
        sheets[seat] = updated;
        var next = state with { Sheets = sheets, RollsUsed = 0, Dice = NoDice, HeldMask = 0 };

        return AllActiveComplete(next)
            ? next with { Status = KniffelStatus.Finished }
            : next with { CurrentSeat = NextSeat(next, seat) };
    }

    private static bool AllActiveComplete(KniffelState state) =>
        Enumerable.Range(0, state.Sheets.Count).Where(seat => !state.Removed[seat]).All(seat => KniffelScoring.IsComplete(state.Sheets[seat]));

    /// <summary>Next seat in order that is not removed and has open categories.</summary>
    private static int NextSeat(KniffelState state, int from)
    {
        var count = state.Sheets.Count;
        for (var step = 1; step <= count; step++)
        {
            var candidate = (from + step) % count;
            if (!state.Removed[candidate] && !KniffelScoring.IsComplete(state.Sheets[candidate]))
            {
                return candidate;
            }
        }

        return from;
    }
}
