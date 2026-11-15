using System.Text.Json;

namespace LlmJson.Tests.Errors;

public class ErrorHandlingTests
{
    [Theory]
    [InlineData("")]
    [InlineData("    ")]
    [InlineData("xyz")]
    [InlineData("-")]
    public void InvalidInputThrowsTheTypedException(string input)
    {
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => PartialJson.Parse(input));
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.IsAssignableFrom<JsonException>(failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("xyz")]
    [InlineData("-")]
    public void TryParseReturnsFalseInsteadOfThrowing(string input)
    {
        Assert.False(PartialJson.TryParse(input, out JsonElement snapshot));
        Assert.Equal(default, snapshot);
    }

    [Fact]
    public void DisallowedTopLevelPartialThrowsNotHangs()
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse("\"abc", new PartialOptions { Allow = Allow.None }));
    }

    [Fact]
    public void IncompleteFailureMessageNamesTheActiveAllowFlags()
    {
        var options = new PartialOptions { Allow = Allow.Numbers | Allow.Booleans };
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => PartialJson.Parse("\"abc", options));
        Assert.Contains("Numbers", failure.Message, StringComparison.Ordinal);
        Assert.Contains("Booleans", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedContentInsideAnAllowedContainerIsDroppedLikeNpm()
    {
        TestSupport.AssertJsonEqual("{}", PartialJson.Parse("{]"));
        TestSupport.AssertJsonEqual("[1]", PartialJson.Parse("[1, oops, 2]"));
    }

    [Fact]
    public void MalformedContentInsideADisallowedContainerThrows()
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse("{]", new PartialOptions { Allow = Allow.None }));
    }

    [Fact]
    public void RepairFailureIsTyped()
    {
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => JsonRepair.Repair("no json in sight"));
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
    }

    [Fact]
    public void CoerceFailureCarriesTheUnderlyingCause()
    {
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => Llm.Coerce("plain prose"));
        Assert.NotNull(failure.InnerException);
        Assert.IsAssignableFrom<JsonException>(failure.InnerException);
    }

    [Fact]
    public void DeserializeBindingFailurePropagatesAsJsonException()
    {
        Assert.ThrowsAny<JsonException>(() => PartialJson.Deserialize<int>("\"not a number\""));
    }

    [Fact]
    public void ExceptionConstructorsPreserveMessageAndInner()
    {
        var inner = new InvalidOperationException("root cause");
        var withInner = new LlmJsonException("outer", inner);
        Assert.Equal("outer", withInner.Message);
        Assert.Same(inner, withInner.InnerException);
        Assert.Equal("solo", new LlmJsonException("solo").Message);
    }
}
