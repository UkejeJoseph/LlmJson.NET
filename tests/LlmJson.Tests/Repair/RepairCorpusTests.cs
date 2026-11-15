namespace LlmJson.Tests.Repair;

public class RepairCorpusTests
{
    public static TheoryData<string, string> Corpus()
    {
        return new TheoryData<string, string>
        {
            { "```json\n{\"a\":1}\n```", "{\"a\":1}" },
            { "```json\r\n{\"a\": 1}\r\n```", "{\"a\": 1}" },
            { "```\n[1,2]\n```", "[1,2]" },
            { "```json\n{\"a\":1}", "{\"a\":1}" },
            { "{'a': 1}", "{\"a\":1}" },
            { "{'a': 'b'}", "{\"a\":\"b\"}" },
            { "{a: 1}", "{\"a\":1}" },
            { "{my_key: \"v\"}", "{\"my_key\":\"v\"}" },
            { "[1, 2, 3,]", "[1,2,3]" },
            { "{\"a\": 1,}", "{\"a\":1}" },
            { "{\"a\": True, \"b\": False, \"c\": None}", "{\"a\":true,\"b\":false,\"c\":null}" },
            { "{\"a\": TRUE, \"b\": FALSE, \"c\": NULL}", "{\"a\":true,\"b\":false,\"c\":null}" },
            { "{\"a\": undefined}", "{\"a\":null}" },
            { "{\"a\": NaN}", "{\"a\":null}" },
            { "{\"a\": Infinity, \"b\": -Infinity}", "{\"a\":null,\"b\":null}" },
            { "{// comment\n\"a\":1}", "{\"a\":1}" },
            { "{\"a\":1 /* block */ , \"b\":2}", "{\"a\":1,\"b\":2}" },
            { "// leading\n[1]", "[1]" },
            { "{“a”: “b”}", "{\"a\":\"b\"}" },
            { "{‘a’: ‘b’}", "{\"a\":\"b\"}" },
            { "{\"a\": \"line1\nline2\"}", "{\"a\":\"line1\\nline2\"}" },
            { "{\"a\": \"tab\there\"}", "{\"a\":\"tab\\there\"}" },
            { "{\"a\":1 \"b\":2}", "{\"a\":1,\"b\":2}" },
            { "[1 2]", "[1,2]" },
            { "{\"a\":\"x\"\n\"b\":\"y\"}", "{\"a\":\"x\",\"b\":\"y\"}" },
            { "[\"a\" \"b\"]", "[\"a\",\"b\"]" },
            { "```json\n{'a': 'b',}\n```", "{\"a\":\"b\"}" },
            { "{\"a\": \"val", "{\"a\":\"val\"}" },
            { "[1, 2", "[1,2]" },
            { "{'a': [1,", "{\"a\":[1]}" },
            { "{'name': 'Ada', 'ok': True, 'x': None}", "{\"name\":\"Ada\",\"ok\":true,\"x\":null}" },
            { "{'msg': 'don't stop'}", "{\"msg\":\"don't stop\"}" },
            { "{\"status\": ok}", "{\"status\":\"ok\"}" },
            { "{\"msg\": hello world}", "{\"msg\":\"hello world\"}" },
            { "{\"when\": 2026-07-20}", "{\"when\":\"2026-07-20\"}" },
            { "[+1, .5, 5.]", "[1,0.5,5]" },
            { "[1, 2}", "[1,2]" },
            { "{\"a\":\"b\"]", "{\"a\":\"b\"}" },
            { "True", "true" },
            { "None", "null" },
            { "{\"quote\": 'he said \"hi\"'}", "{\"quote\":\"he said \\\"hi\\\"\"}" },
            { "{'a': {'b': [1, 2,]},}", "{\"a\":{\"b\":[1,2]}}" },
            { "```json\n{\"a\": \"cut off he", "{\"a\":\"cut off he\"}" },
            { "{\"a\":1}{\"b\":2}", "[{\"a\":1},{\"b\":2}]" },
            { "{\"a\":1}\n{\"b\":2}\n{\"c\":3}", "[{\"a\":1},{\"b\":2},{\"c\":3}]" },
            { "{\"a\":1},{\"b\":2}", "[{\"a\":1},{\"b\":2}]" },
            { "[1][2]", "[[1],[2]]" },
            { "[1, 2, 3, ...]", "[1,2,3]" },
            { "[1, 2, …]", "[1,2]" },
            { "{'a': ..., 'b': 1}", "{\"b\":1}" },
            { "{\"a\": \"end\\", "{\"a\":\"end\"}" },
            { "{'a': 'end\\", "{\"a\":\"end\"}" },
            { "{'x': 1.2e}", "{\"x\":1.2}" },
            { "{'x': 1.2e", "{\"x\":1.2}" },
            { "{'x': 1e2ee}", "{\"x\":1e2}" },
            { "['a' 'b',]", "[\"a\",\"b\"]" },
            { "{'active': True, 'note': None, 'tags': ['a' 'b',],}", "{\"active\":true,\"note\":null,\"tags\":[\"a\",\"b\"]}" },
            { "{'a': 'x' 'b': 'y'}", "{\"a\":\"x\",\"b\":\"y\"}" },
        };
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void DirtyInputIsRepaired(string dirty, string expected)
    {
        Assert.Equal(expected, JsonRepair.Repair(dirty));
    }

    [Theory]
    [MemberData(nameof(Corpus))]
    public void RepairIsIdempotent(string dirty, string expected)
    {
        Assert.Equal(expected, JsonRepair.Repair(JsonRepair.Repair(dirty)));
    }

    public static TheoryData<string> AlreadyValid()
    {
        return new TheoryData<string>
        {
            "{\"a\": 1}",
            " [1, 2] ",
            "\"str\"",
            "3.14",
            "null",
            "true",
            "{\"nested\":{\"x\":[1,2,3]}} ",
            "{\"esc\":\"a\\nb\\u00e9\"}",
        };
    }

    [Theory]
    [MemberData(nameof(AlreadyValid))]
    public void ValidJsonPassesThroughByteIdentical(string valid)
    {
        Assert.Same(valid, JsonRepair.Repair(valid));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("just some prose without structure")]
    [InlineData("// only a comment")]
    public void UnrepairableInputReturnsFalse(string? dirty)
    {
        Assert.False(JsonRepair.TryRepair(dirty, out string? repaired));
        Assert.Null(repaired);
    }

    [Fact]
    public void UnrepairableInputThrowsTyped()
    {
        Assert.Throws<LlmJsonException>(() => JsonRepair.Repair("just some prose without structure"));
    }

    [Theory]
    [InlineData("{'coords': (1, 2)}")]
    [InlineData("{'point': ('x', 'y'), 'n': 1}")]
    public void PythonTuplesFailLoudlyInsteadOfMangling(string dirty)
    {
        Assert.Throws<LlmJsonException>(() => JsonRepair.Repair(dirty));
        Assert.False(JsonRepair.TryRepair(dirty, out _));
    }

    [Fact]
    public void EllipsisElementsAreDroppedAndTheResultBindsToTypes()
    {
        int[]? values = Llm.CoerceTo<int[]>("[1, 2, 3, ...]");
        Assert.Equal([1, 2, 3], values);
    }
}
