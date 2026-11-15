namespace LlmJson.Tests.Partial;

public class UnicodeTruncationTests
{
    public static TheoryData<string, string> Cases()
    {
        return new TheoryData<string, string>
        {
            { "\"abc\\", "\"abc\"" },
            { "\"abc\\u", "\"abc\"" },
            { "\"abc\\u0", "\"abc\"" },
            { "\"abc\\u00", "\"abc\"" },
            { "\"abc\\u00e", "\"abc\"" },
            { "\"abc\\u00e9", "\"abcé\"" },
            { "\"a\\ud83d", "\"a\"" },
            { "\"a\\ud83d\\u", "\"a\"" },
            { "\"a\\ud83d\\ude0", "\"a\"" },
            { "\"a\\ud83d\\ude00", "\"a😀\"" },
            { "\"a\ud83d", "\"a\"" },
            { "\"a😀", "\"a😀\"" },
            { "\"a😀b", "\"a😀b\"" },
            { "\"tab\\t", "\"tab\\t\"" },
            { "\"esc\\\\", "\"esc\\\\\"" },
            { "\"quote\\\"", "\"quote\\\"\"" },
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TruncationMidEscapeOrMidCodepointDropsTheIncompleteTail(string truncated, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated));
    }

    [Fact]
    public void InvalidEscapeTailTruncatesAtTheEscapeLikeNpm()
    {
        TestSupport.AssertJsonEqual("\" \"", PartialJson.Parse("\" \\x12"));
    }

    [Fact]
    public void RawNewlineInsideAnUnterminatedStringTruncatesBeforeIt()
    {
        TestSupport.AssertJsonEqual("\"a\"", PartialJson.Parse("\"a\nb"));
    }

    [Fact]
    public void LoneSurrogateInsideAClosedStringIsRejected()
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse("\"a\ud83db\""));
    }

    [Fact]
    public void SurrogatePairSplitInsideAnObjectValueKeepsTheSafePrefix()
    {
        TestSupport.AssertJsonEqual("""{"e":"a"}""", PartialJson.Parse("{\"e\":\"a\ud83d"));
    }
}
