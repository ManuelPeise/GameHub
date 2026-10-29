using Logic.Core.Abstractions;

namespace Logic.Core.Randomness;

/// <summary>Deterministic <see cref="IRandom"/>: the same seed always gives the same values.</summary>
public sealed class SeededRandom : IRandom
{
    private readonly System.Random _random;

    public SeededRandom(int seed)
    {
        // System.Random with an explicit seed uses a fixed algorithm that is stable across platforms.
        _random = new System.Random(seed);
    }

    /// <summary>
    /// Random source for one action of a session. Each action gets its own seed derived from the session
    /// seed and the action number, so a replay can recreate the draws of any action independently.
    /// </summary>
    public static SeededRandom ForAction(long sessionSeed, int sequence)
    {
        unchecked
        {
            var mixed = (sessionSeed * 6364136223846793005L) + (sequence * 1442695040888963407L);
            return new SeededRandom((int)(mixed ^ (mixed >> 32)));
        }
    }

    /// <summary>
    /// Random source for the AI decision before an action. Separate from <see cref="ForAction"/>, so the
    /// AI's choices are not correlated with the draws of the domain (e.g. it cannot "know" upcoming dice).
    /// </summary>
    public static SeededRandom ForAiDecision(long sessionSeed, int sequence)
    {
        unchecked
        {
            return ForAction(sessionSeed ^ AiStreamSalt, sequence);
        }
    }

    private const long AiStreamSalt = 0x5DEECE66DL;

    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxExclusive);
        return _random.Next(maxExclusive);
    }
}
