using Logic.Core.Model;
using Logic.Core.Randomness;
using Logic.TicTacToe;
using Logic.TicTacToe.Domain;

namespace Tests.TicTacToe;

public sealed class TicTacToeModuleTests
{
    private readonly TicTacToeModule _module = new();
    private readonly SeededRandom _random = new(1);

    [Xunit.Fact]
    public void JsonRoundTrip_KeepsTheState()
    {
        var json = _module.CreateInitialState(2, _random);
        var applied = Xunit.Assert.IsType<ActionResult.Applied>(_module.Apply(json, 0, "PlaceMark", "{\"cell\":4}", _random));

        var state = _module.DeserializeState(applied.StateJson);

        Xunit.Assert.Equal("----X----", state.Board);
        Xunit.Assert.Equal(applied.StateJson, _module.SerializeState(state));
        Xunit.Assert.Contains("\"status\":\"Running\"", applied.StateJson, StringComparison.Ordinal);
    }

    [Xunit.Theory]
    [Xunit.InlineData("Roll", "{\"cell\":4}")]
    [Xunit.InlineData("PlaceMark", null)]
    [Xunit.InlineData("PlaceMark", "not json")]
    public void UnknownOrMalformedAction_IsRejected(string actionType, string? payload)
    {
        var json = _module.CreateInitialState(2, _random);

        Xunit.Assert.IsType<ActionResult.Rejected>(_module.Apply(json, 0, actionType, payload, _random));
    }

    [Xunit.Fact]
    public void InvalidMove_IsRejectedWithReason()
    {
        var json = _module.CreateInitialState(2, _random);

        var rejected = Xunit.Assert.IsType<ActionResult.Rejected>(_module.Apply(json, 1, "PlaceMark", "{\"cell\":0}", _random));

        Xunit.Assert.Equal("Du bist nicht am Zug.", rejected.Error);
    }

    [Xunit.Fact]
    public void SerializeAction_IsAcceptedByApply()
    {
        var (actionType, payload) = _module.SerializeAction(new PlaceMark(8));

        var applied = _module.Apply(_module.CreateInitialState(2, _random), 0, actionType, payload, _random);

        Xunit.Assert.IsType<ActionResult.Applied>(applied);
    }
}
