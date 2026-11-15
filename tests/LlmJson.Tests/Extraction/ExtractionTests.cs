namespace LlmJson.Tests.Extraction;

public class ExtractionTests
{
    [Theory]
    [InlineData("Here is the data: {\"a\":1}. Thanks!", "{\"a\":1}")]
    [InlineData("Options are [1,2] and {\"a\":3}", "[1,2]")]
    [InlineData("{\"a\":1}", "{\"a\":1}")]
    [InlineData("prefix {\"code\": \"if (x) { y(); }\"} suffix", "{\"code\": \"if (x) { y(); }\"}")]
    [InlineData("prefix {\"nested\": {\"x\": [1, 2]}} suffix", "{\"nested\": {\"x\": [1, 2]}}")]
    public void FirstBalancedValueIsExtracted(string prose, string expected)
    {
        Assert.Equal(expected, Llm.ExtractJson(prose));
    }

    [Fact]
    public void FencedJsonIsPreferredOverEarlierProseBraces()
    {
        string prose = "I checked {this} first.\n```json\n{\"a\": 1}\n```\nDone.";
        Assert.Equal("{\"a\": 1}", Llm.ExtractJson(prose));
    }

    [Fact]
    public void TruncatedTailIsReturnedForMidGenerationCutoff()
    {
        Assert.Equal("{\"a\": [1, 2", Llm.ExtractJson("Result: {\"a\": [1, 2"));
    }

    [Theory]
    [InlineData("no json here at all")]
    [InlineData("")]
    [InlineData("1 + 1 = 2")]
    public void ProseWithoutAValueReturnsNull(string prose)
    {
        Assert.Null(Llm.ExtractJson(prose));
    }

    [Fact]
    public void SingleQuotedStringsDoNotBreakBalanceScanning()
    {
        string prose = "data: {'a': 'b}c'} end";
        Assert.Equal("{'a': 'b}c'}", Llm.ExtractJson(prose));
    }
}
