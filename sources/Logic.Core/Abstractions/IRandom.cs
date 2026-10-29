namespace Logic.Core.Abstractions;

/// <summary>
/// Source of randomness for game rules and AI (dice, random moves). Injected so that games are
/// reproducible with a seed and testable with fixed values.
/// </summary>
public interface IRandom
{
    /// <summary>Returns a value from 0 (inclusive) to <paramref name="maxExclusive"/> (exclusive).</summary>
    int Next(int maxExclusive);
}
