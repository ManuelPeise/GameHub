using Logic.Core.Model;
using Logic.Shared.Models;
using Shared.Models.Games;

namespace Logic.Sessions;

/// <summary>Maps the result of a finished game to the seats; removed seats are not scored.</summary>
internal static class SessionResults
{
    public static List<SeatResultWrite> Map(IEnumerable<SeatSnapshot> seats, GameProgress progress) =>
    [
        .. seats.Select(seat =>
        {
            if (seat.ConnectionStatus == SeatConnectionStatus.Removed)
            {
                return new SeatResultWrite(seat.Id, GameOutcome.Removed, null, null);
            }

            var result = progress.Results.FirstOrDefault(r => r.Seat == seat.Order);
            return result is null
                ? new SeatResultWrite(seat.Id, GameOutcome.Removed, null, null)
                : new SeatResultWrite(seat.Id, MapOutcome(result.Outcome), result.Placement, result.Score);
        }),
    ];

    /// <summary>Only one active seat is left: it wins, all removed seats get no placement.</summary>
    public static List<SeatResultWrite> LastSeatStanding(IEnumerable<SeatSnapshot> seats) =>
    [
        .. seats.Select(seat => seat.ConnectionStatus == SeatConnectionStatus.Removed
            ? new SeatResultWrite(seat.Id, GameOutcome.Removed, null, null)
            : new SeatResultWrite(seat.Id, GameOutcome.Win, 1, null)),
    ];

    private static GameOutcome MapOutcome(SeatOutcome outcome) => outcome switch
    {
        SeatOutcome.Win => GameOutcome.Win,
        SeatOutcome.Loss => GameOutcome.Loss,
        _ => GameOutcome.Draw,
    };
}
