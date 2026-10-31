namespace Logic.Muehle.Domain;

/// <summary>
/// Geometry of the Mühle board: 24 points on three squares (0–7 outer, 8–15 middle, 16–23 inner),
/// each numbered clockwise from the top left corner: corner, middle of the side, corner, …
/// The middles of the sides (odd numbers) connect the squares.
/// </summary>
public static class MuehleBoard
{
    public const int PointCount = 24;

    /// <summary>Neighbours of every point.</summary>
    public static IReadOnlyList<IReadOnlyList<int>> Neighbours { get; } = BuildNeighbours();

    /// <summary>All 16 mills (12 along the sides, 4 across the squares).</summary>
    public static IReadOnlyList<int[]> Mills { get; } = BuildMills();

    /// <summary>The mills every point belongs to (always two).</summary>
    public static IReadOnlyList<IReadOnlyList<int[]>> MillsOfPoint { get; } =
        [.. Enumerable.Range(0, PointCount).Select(point => (IReadOnlyList<int[]>)[.. Mills.Where(mill => mill.Contains(point))])];

    private static IReadOnlyList<IReadOnlyList<int>> BuildNeighbours()
    {
        var neighbours = Enumerable.Range(0, PointCount).Select(_ => new List<int>()).ToArray();
        for (var ring = 0; ring < 3; ring++)
        {
            for (var index = 0; index < 8; index++)
            {
                var point = (ring * 8) + index;
                var next = (ring * 8) + ((index + 1) % 8);
                neighbours[point].Add(next);
                neighbours[next].Add(point);

                if (index % 2 == 1 && ring < 2)
                {
                    neighbours[point].Add(point + 8);
                    neighbours[point + 8].Add(point);
                }
            }
        }

        return [.. neighbours.Select(list => (IReadOnlyList<int>)[.. list.Order()])];
    }

    private static IReadOnlyList<int[]> BuildMills()
    {
        var mills = new List<int[]>();
        for (var ring = 0; ring < 3; ring++)
        {
            for (var corner = 0; corner < 8; corner += 2)
            {
                mills.Add([(ring * 8) + corner, (ring * 8) + corner + 1, (ring * 8) + ((corner + 2) % 8)]);
            }
        }

        for (var middle = 1; middle < 8; middle += 2)
        {
            mills.Add([middle, middle + 8, middle + 16]);
        }

        return mills;
    }
}
