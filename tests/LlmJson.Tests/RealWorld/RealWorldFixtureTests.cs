using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlmJson.Tests.RealWorld;

public class RealWorldFixtureTests
{
    private static string FixtureDirectory => Path.Combine(AppContext.BaseDirectory, "fixtures", "realworld");

    public static TheoryData<string> FixtureNames()
    {
        return new TheoryData<string>
        {
            "fenced_tool_call",
            "chatty_preamble",
            "python_dict",
            "truncated_mid_string",
            "truncated_mid_array",
            "dirty_combo",
            "fence_no_lang_crlf",
        };
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public void CapturedLlmOutputCoercesToTheExpectedJson(string name)
    {
        string raw = File.ReadAllText(Path.Combine(FixtureDirectory, name + ".txt"));
        string expected = File.ReadAllText(Path.Combine(FixtureDirectory, "expected", name + ".json"));

        JsonElement coerced = Llm.Coerce(raw);
        Assert.True(
            JsonNode.DeepEquals(JsonNode.Parse(expected), JsonNode.Parse(coerced.GetRawText())),
            $"Fixture {name} coerced to {coerced.GetRawText()}");
    }

    [Fact]
    public void ThereAreAtLeastSixFixtures()
    {
        Assert.True(Directory.GetFiles(FixtureDirectory, "*.txt").Length >= 6);
    }

    [Fact]
    public void TruncatedFixturesAlsoWorkThroughTryCoerce()
    {
        foreach (string name in new[] { "truncated_mid_string", "truncated_mid_array" })
        {
            string raw = File.ReadAllText(Path.Combine(FixtureDirectory, name + ".txt"));
            Assert.True(Llm.TryCoerce(raw, out _), $"Fixture {name} did not coerce");
        }
    }
}
