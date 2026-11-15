using System.Text.Json;

namespace LlmJson.Tests.Errors;

public class FuzzNoThrowTests
{
    private const char HighSurrogate = '\ud83d';
    private const char LowSurrogate = '\ude00';

    private static readonly string[] Templates =
    [
        """{"a": "X", "b": [1, 2, "Y"]}""",
        "```json\n{'k': 'value'}\n```",
        """prefix {"m": "text"} suffix""",
        """{"emoji": "hi """,
        "[1, 2e, tru",
    ];

    public static TheoryData<int, int, bool> MutationPoints()
    {
        var data = new TheoryData<int, int, bool>();
        for (int template = 0; template < Templates.Length; template++)
        {
            for (int position = 0; position <= Templates[template].Length; position++)
            {
                data.Add(template, position, true);
                data.Add(template, position, false);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(MutationPoints))]
    public void TryVariantsNeverThrowOnLoneSurrogateMutations(int template, int position, bool high)
    {
        string input = Templates[template].Insert(position, (high ? HighSurrogate : LowSurrogate).ToString());

        PartialJson.TryParse(input, out _);
        JsonRepair.TryRepair(input, out _);
        Llm.TryCoerce(input, out _);
    }

    [Fact]
    public void TruncatedEmojiInputSucceedsThroughEveryTryApi()
    {
        string input = "{\"emoji\": \"hi \ud83d";

        Assert.True(PartialJson.TryParse(input, out JsonElement snapshot));
        TestSupport.AssertJsonEqual("""{"emoji":"hi "}""", snapshot);

        Assert.True(JsonRepair.TryRepair(input, out string? repaired));
        Assert.Equal("""{"emoji":"hi "}""", repaired);

        Assert.True(Llm.TryCoerce(input, out JsonElement coerced));
        TestSupport.AssertJsonEqual("""{"emoji":"hi "}""", coerced);
    }

    [Fact]
    public void LoneSurrogateInTheMiddleOfARepairedStringBecomesReplacementChar()
    {
        Assert.True(JsonRepair.TryRepair("{'a': 'x\ud83dy'}", out string? repaired));
        Assert.Equal("{\"a\":\"x�y\"}", repaired);
    }

    [Theory]
    [InlineData("\ud83d")]
    [InlineData("\ude00\ude00")]
    [InlineData("{\"\ud83d\": 1}")]
    [InlineData("[\"\ude00\"]")]
    public void PureSurrogateGarbageNeverThrowsFromTryApis(string input)
    {
        PartialJson.TryParse(input, out _);
        JsonRepair.TryRepair(input, out _);
        Llm.TryCoerce(input, out _);
    }
}
