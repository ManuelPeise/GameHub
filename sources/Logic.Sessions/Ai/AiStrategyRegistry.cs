using Logic.Core.Ai;

namespace Logic.Sessions.Ai;

public sealed class AiStrategyRegistry(IEnumerable<IAiPlayer> players) : IAiStrategyRegistry
{
    private readonly Dictionary<(string GameType, string Difficulty), IAiPlayer> _players = players
        .ToDictionary(
            player => (player.GameType, player.Difficulty),
            StringTupleComparer.OrdinalIgnoreCase);

    public IAiPlayer Get(string gameType, string difficulty) =>
        _players.TryGetValue((gameType, difficulty), out var player)
            ? player
            : throw new InvalidOperationException(
                $"No AI strategy registered for game '{gameType}' and difficulty '{difficulty}'.");

    private sealed class StringTupleComparer : IEqualityComparer<(string GameType, string Difficulty)>
    {
        public static StringTupleComparer OrdinalIgnoreCase { get; } = new();

        public bool Equals((string GameType, string Difficulty) x, (string GameType, string Difficulty) y) =>
            string.Equals(x.GameType, y.GameType, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Difficulty, y.Difficulty, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string GameType, string Difficulty) value)
        {
            var hash = new HashCode();
            hash.Add(value.GameType, StringComparer.OrdinalIgnoreCase);
            hash.Add(value.Difficulty, StringComparer.OrdinalIgnoreCase);
            return hash.ToHashCode();
        }
    }
}
