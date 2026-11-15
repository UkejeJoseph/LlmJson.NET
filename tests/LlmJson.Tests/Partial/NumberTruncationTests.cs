namespace LlmJson.Tests.Partial;

public class NumberTruncationTests
{
    public static TheoryData<string, Allow, string> Cases()
    {
        return new TheoryData<string, Allow, string>
        {
            { "1.5e", Allow.Numbers, "1.5" },
            { "1.5e+", Allow.Numbers, "1.5" },
            { "1.5e-", Allow.Numbers, "1.5" },
            { "1.5E+", Allow.Numbers, "1.5" },
            { "-1.25e", Allow.Numbers, "-1.25" },
            { "1e", Allow.Numbers, "1" },
            { "12345.678", Allow.Numbers, "12345.678" },
            { "0", Allow.None, "0" },
            { "-1.25e+4", Allow.None, "-1.25e+4" },
            { "1.", Allow.Numbers, "1" },
            { "[1.5e", Allow.Arrays | Allow.Numbers, "[1.5]" },
            { "[1.5e", Allow.Arrays, "[]" },
            { "[1.", Allow.Arrays | Allow.Numbers, "[1]" },
            { "[1.", Allow.Arrays, "[]" },
            { "[-", Allow.Arrays | Allow.Numbers, "[]" },
            { "[1e5", Allow.Arrays | Allow.Numbers, "[1e5]" },
            { "[0.25", Allow.Arrays | Allow.Numbers, "[0.25]" },
            { """{"n": 42""", Allow.Objects | Allow.Numbers, """{"n":42}""" },
            { """{"n": 42""", Allow.Objects, "{}" },
            { """{"n": 4.2e""", Allow.Objects | Allow.Numbers, """{"n":4.2}""" },
            { "[1, -2.5, 3", Allow.Arrays | Allow.Numbers, "[1,-2.5,3]" },
            { "1e2ee", Allow.Numbers, "1e2" },
            { "-1.2e+-3", Allow.Numbers, "-1.2" },
            { "[3.4.5", Allow.Arrays | Allow.Numbers, "[]" },
            { "[1e2ee", Allow.Arrays | Allow.Numbers, "[1e2]" },
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TruncatedNumbersFollowNpmSemantics(string truncated, Allow allow, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated, new PartialOptions { Allow = allow }));
    }

    [Theory]
    [InlineData("-")]
    [InlineData("1e+x")]
    [InlineData("01")]
    public void UncompletableTopLevelNumbersThrow(string truncated)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated, new PartialOptions { Allow = Allow.Numbers }));
    }

    [Fact]
    public void CompleteNumberFollowedByDelimiterNeedsNoNumbersFlag()
    {
        TestSupport.AssertJsonEqual("[7]", PartialJson.Parse("[7,", new PartialOptions { Allow = Allow.Arrays }));
    }
}
