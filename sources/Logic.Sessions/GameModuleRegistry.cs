using Logic.Core.Abstractions;

namespace Logic.Sessions;

/// <summary>All registered game modules, looked up by game type.</summary>
public sealed class GameModuleRegistry
{
    private readonly Dictionary<string, IGameModule> _modules;

    public GameModuleRegistry(IEnumerable<IGameModule> modules)
    {
        ArgumentNullException.ThrowIfNull(modules);
        _modules = modules.ToDictionary(module => module.GameType, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> GameTypes => _modules.Keys;

    public bool TryGet(string gameType, out IGameModule module) => _modules.TryGetValue(gameType, out module!);

    public IGameModule Get(string gameType) =>
        TryGet(gameType, out var module)
            ? module
            : throw new InvalidOperationException($"No game module registered for game type '{gameType}'.");
}
