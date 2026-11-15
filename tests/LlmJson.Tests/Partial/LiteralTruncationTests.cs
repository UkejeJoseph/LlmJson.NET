namespace LlmJson.Tests.Partial;

public class LiteralTruncationTests
{
    public static TheoryData<string, Allow, string> Cases()
    {
        var data = new TheoryData<string, Allow, string>();
        string[] literals = ["true", "false", "null"];
        foreach (string literal in literals)
        {
            Allow needed = literal == "null" ? Allow.Null : Allow.Booleans;
            for (int length = 1; length < literal.Length; length++)
            {
                data.Add(literal[..length], needed, literal);
                data.Add($"[{literal[..length]}", Allow.Arrays | needed, $"[{literal}]");
                data.Add($"[{literal[..length]}", Allow.Arrays, "[]");
            }

            data.Add(literal, Allow.None, literal);
            data.Add($"[{literal}", Allow.Arrays, $"[{literal}]");
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void TruncatedLiteralsCompletePerFlags(string truncated, Allow allow, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated, new PartialOptions { Allow = allow }));
    }

    [Theory]
    [InlineData("tr", Allow.Null)]
    [InlineData("fa", Allow.Null)]
    [InlineData("nu", Allow.Booleans)]
    public void PrefixWithoutItsFlagThrowsAtTopLevel(string truncated, Allow allow)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated, new PartialOptions { Allow = allow }));
    }

    [Theory]
    [InlineData("trap")]
    [InlineData("nulk")]
    public void NonLiteralWordsAreMalformed(string truncated)
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(truncated));
    }

    [Theory]
    [InlineData("falsey", "false")]
    [InlineData("true xyz", "true")]
    [InlineData("[1,2] trailing prose", "[1,2]")]
    public void ContentAfterACompleteRootValueIsIgnoredAsInNpm(string truncated, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated));
    }
}
