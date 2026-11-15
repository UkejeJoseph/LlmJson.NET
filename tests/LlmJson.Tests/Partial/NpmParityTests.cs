namespace LlmJson.Tests.Partial;

public class NpmParityTests
{
    private static PartialOptions With(Allow allow) => new() { Allow = allow };

    public static TheoryData<string, Allow, string> ParityCases()
    {
        return new TheoryData<string, Allow, string>
        {
            { "\"", Allow.Strings, "\"\"" },
            { "\" \\x12", Allow.Strings, "\" \"" },
            { "[\"", Allow.Arrays, "[]" },
            { "[\"", Allow.Arrays | Allow.Strings, "[\"\"]" },
            { "{\"\": \"", Allow.Objects, "{}" },
            { "{\"\": \"", Allow.Objects | Allow.Strings, "{\"\":\"\"}" },
            { "n", Allow.Null, "null" },
            { "t", Allow.Booleans, "true" },
            { "f", Allow.Booleans, "false" },
            { "0", Allow.All & ~Allow.Numbers, "0" },
            { "-1.25e+4", Allow.All & ~Allow.Numbers, "-1.25e+4" },
            { "-1.25e+", Allow.Numbers, "-1.25" },
            { "-1.25e", Allow.Numbers, "-1.25" },
        };
    }

    public static TheoryData<string, Allow> ThrowingCases()
    {
        return new TheoryData<string, Allow>
        {
            { "\"", Allow.All & ~Allow.Strings },
            { "[", Allow.Strings },
            { "[\"", Allow.Strings },
            { "[\"\"", Allow.Strings },
            { "[\"\",", Allow.Strings },
            { "{", Allow.Strings },
            { "{\"", Allow.Strings },
            { "{\"\"", Allow.Strings },
            { "{\"\":", Allow.Strings },
            { "{\"\":\"", Allow.Strings },
            { "{\"\":\"\"", Allow.Strings },
            { "n", Allow.All & ~Allow.Null },
            { "t", Allow.All & ~Allow.Booleans },
            { "f", Allow.All & ~Allow.Booleans },
            { "-1.25e+", Allow.All & ~Allow.Numbers },
            { "-", Allow.All },
        };
    }

    [Theory]
    [MemberData(nameof(ParityCases))]
    public void MatchesNpmPartialJson(string truncated, Allow allow, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated, With(allow)));
    }

    [Theory]
    [MemberData(nameof(ThrowingCases))]
    public void ThrowsWhereNpmPartialJsonThrows(string truncated, Allow allow)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated, With(allow)));
        Assert.False(PartialJson.TryParse(truncated, out _, With(allow)));
    }

    [Theory]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("NaN")]
    [InlineData("I")]
    [InlineData("N")]
    public void NonJsonNumericLiteralsAreRejectedUnlikeNpm(string truncated)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \t\r\n")]
    public void EmptyInputThrows(string truncated)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated));
    }
}
