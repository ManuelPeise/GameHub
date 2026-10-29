using Logic.Core.Abstractions;

namespace Logic.Core.Randomness;

/// <summary>Wraps a random source and records every value drawn (stored with the action in the move log).</summary>
public sealed class RecordingRandom(IRandom inner) : IRandom
{
    private readonly List<int> _draws = [];

    public IReadOnlyList<int> Draws => _draws;

    public int Next(int maxExclusive)
    {
        var value = inner.Next(maxExclusive);
        _draws.Add(value);
        return value;
    }
}
