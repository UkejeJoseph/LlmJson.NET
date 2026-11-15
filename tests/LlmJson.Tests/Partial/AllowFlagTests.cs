namespace LlmJson.Tests.Partial;

public class AllowFlagTests
{
    public static TheoryData<string, Allow, string> Cases()
    {
        return new TheoryData<string, Allow, string>
        {
            { """{"a": {"b": "x""", Allow.Objects, """{"a":{}}""" },
            { """{"a": {"b": "x""", Allow.Objects | Allow.Strings, """{"a":{"b":"x"}}""" },
            { "[[\"", Allow.Arrays, "[[]]" },
            { """{"a": [1, 2""", Allow.Objects, "{}" },
            { """{"a": [1, 2""", Allow.Objects | Allow.Arrays, """{"a":[1]}""" },
            { """{"a": [1, 2""", Allow.All, """{"a":[1,2]}""" },
            { "[tru", Allow.Arrays, "[]" },
            { "[tru", Allow.Arrays | Allow.Booleans, "[true]" },
            { "[true", Allow.Arrays, "[true]" },
            { "[false", Allow.Arrays, "[false]" },
            { "[null", Allow.Arrays, "[null]" },
            { "[nul", Allow.Arrays, "[]" },
            { "[nul", Allow.Arrays | Allow.Null, "[null]" },
            { "[1,", Allow.Arrays, "[1]" },
            { "[1", Allow.Arrays, "[]" },
            { "[1", Allow.Arrays | Allow.Numbers, "[1]" },
            { """{"a":1,""", Allow.Objects, """{"a":1}""" },
            { """{"a":tru""", Allow.Objects, "{}" },
            { """{"a":tru""", Allow.Objects | Allow.Booleans, """{"a":true}""" },
            { """{"a":1,"b""", Allow.Objects, """{"a":1}""" },
            { """{"a":1,"b":""", Allow.Objects, """{"a":1}""" },
            { """["a", "b""", Allow.Arrays, """["a"]""" },
            { """["a", "b""", Allow.Arrays | Allow.Strings, """["a","b"]""" },
            { "{", Allow.Objects, "{}" },
            { "[", Allow.Arrays, "[]" },
        };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DisallowedPartialsAreOmittedNotCompleted(string truncated, Allow allow, string expected)
    {
        TestSupport.AssertJsonEqual(expected, PartialJson.Parse(truncated, new PartialOptions { Allow = allow }));
    }

    [Fact]
    public void DisallowedUnclosedSubtreeIsDroppedWhenAncestorAllows()
    {
        TestSupport.AssertJsonEqual("{}", PartialJson.Parse("""{"a": ["x""", new PartialOptions { Allow = Allow.Objects }));
    }

    [Fact]
    public void DisallowedUnclosedRootThrowsEvenWhenChildCompleted()
    {
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse("[[\"", new PartialOptions { Allow = Allow.Strings }));
    }

    [Fact]
    public void CompleteDocumentParsesUnderAllowNone()
    {
        TestSupport.AssertJsonEqual("""{"a":[1,true,null,"x"]}""", PartialJson.Parse("""{"a":[1,true,null,"x"]}""", new PartialOptions { Allow = Allow.None }));
    }
}
