using Logic.Core.Abstractions;

namespace Logic.Core.Ai;

/// <summary>Known AI difficulty levels as extensible string values.</summary>
public static class AiDifficulties
{
    public const string Easy = "Easy";
    public const string Medium = "Medium";
    public const string Hard = "Hard";
}

/// <summary>Typed AI strategy for one game view and one action type.</summary>
public interface IAiStrategy<in TView, out TAction>
{
    string Difficulty { get; }

    string Version { get; }

    TAction ChooseAction(TView view, int seat, IRandom random);
}

/// <summary>
/// Non-generic bridge used by the session layer. State in and action out are JSON-compatible values.
/// </summary>
public interface IAiPlayer
{
    string GameType { get; }

    string Difficulty { get; }

    string Version { get; }

    (string ActionType, string? PayloadJson) ChooseAction(string stateJson, int seat, IRandom random);
}

public interface IAiStrategyRegistry
{
    IAiPlayer Get(string gameType, string difficulty);
}
