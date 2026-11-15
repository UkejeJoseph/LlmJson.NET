using System.Text.Json;

namespace LlmJson.Tests.Extraction;

public class CoerceTests
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private sealed record Verdict(string? Label, double Confidence, List<string>? Reasons);

    [Fact]
    public void TheReadmeOneLinerWorks()
    {
        string raw = """
            Sure! Here's my analysis:

            ```json
            {'label': 'positive', 'confidence': 0.87, 'reasons': ['tone', 'word choice',]}
            ```

            Let me know if you need anything else.
            """;
        Verdict? verdict = Llm.CoerceTo<Verdict>(raw, CaseInsensitive);
        Assert.NotNull(verdict);
        Assert.Equal("positive", verdict.Label);
        Assert.Equal(0.87, verdict.Confidence);
        Assert.Equal(["tone", "word choice"], verdict.Reasons);
    }

    [Fact]
    public void ChattyPreambleDirtyJsonAndTruncationAllAtOnce()
    {
        string raw = "Of course! {'items': [{'name': 'first'}, {'name': 'sec";
        JsonElement value = Llm.Coerce(raw);
        TestSupport.AssertJsonEqual("""{"items":[{"name":"first"},{"name":"sec"}]}""", value);
    }

    [Fact]
    public void PlainValidJsonCoercesUnchanged()
    {
        TestSupport.AssertJsonEqual("""{"a":[1,2]}""", Llm.Coerce("""{"a":[1,2]}"""));
    }

    [Fact]
    public void PythonLiteralAtTopLevelCoerces()
    {
        Assert.Equal(JsonValueKind.True, Llm.Coerce("True").ValueKind);
        Assert.Equal(JsonValueKind.Null, Llm.Coerce("None").ValueKind);
    }

    [Fact]
    public void ProseWithoutJsonThrowsWithInnerCause()
    {
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => Llm.Coerce("I could not produce the requested data."));
        Assert.NotNull(failure.InnerException);
    }

    [Fact]
    public void TryCoerceReturnsFalseForProse()
    {
        Assert.False(Llm.TryCoerce("nothing to see here", out _));
    }

    [Fact]
    public void TryCoerceReturnsTrueForMessyInput()
    {
        Assert.True(Llm.TryCoerce("answer: ```{\"ok\": true}```", out JsonElement value));
        Assert.True(value.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public void TryCoerceToBindsARecordFromMessyInput()
    {
        string raw = """
            Sure! Here's my analysis:

            ```json
            {'label': 'positive', 'confidence': 0.87, 'reasons': ['tone', 'word choice',]}
            ```
            """;
        Assert.True(Llm.TryCoerceTo(raw, out Verdict? verdict, CaseInsensitive));
        Assert.NotNull(verdict);
        Assert.Equal("positive", verdict.Label);
        Assert.Equal(0.87, verdict.Confidence);
        Assert.Equal(["tone", "word choice"], verdict.Reasons);
    }

    [Fact]
    public void TryCoerceToBindsAPrimitive()
    {
        Assert.True(Llm.TryCoerceTo("42", out int score));
        Assert.Equal(42, score);
    }

    [Fact]
    public void TryCoerceToReturnsFalseAndDefaultForProse()
    {
        Assert.False(Llm.TryCoerceTo("nothing to see here", out Verdict? verdict, CaseInsensitive));
        Assert.Null(verdict);
    }

    [Fact]
    public void TryCoerceToReturnsFalseAndDefaultForPrimitiveTarget()
    {
        Assert.False(Llm.TryCoerceTo("nothing to see here", out int score));
        Assert.Equal(0, score);
    }

    [Fact]
    public void TryCoerceToThrowsOnNullArgument()
    {
        Assert.Throws<ArgumentNullException>(() => Llm.TryCoerceTo<Verdict>(null!, out _, CaseInsensitive));
    }

    [Fact]
    public void TryCoerceToMatchesCoerceToWhenItSucceeds()
    {
        string raw = "answer: ```{'label': 'neutral', 'confidence': 0.5, 'reasons': ['balanced']}```";
        Assert.True(Llm.TryCoerceTo(raw, out Verdict? tried, CaseInsensitive));
        Verdict? thrown = Llm.CoerceTo<Verdict>(raw, CaseInsensitive);
        Assert.Equal(JsonSerializer.Serialize(thrown), JsonSerializer.Serialize(tried));
    }

    [Fact]
    public void FencedPayloadWinsOverEarlierProseBraces()
    {
        JsonElement value = Llm.Coerce("Weights {vary} per run.\n```json\n[10, 20, 30]\n```");
        TestSupport.AssertJsonEqual("[10,20,30]", value);
    }

    [Fact]
    public void ProseBracesRepairingToEmptyDoNotShadowTheRealPayload()
    {
        JsonElement value = Llm.Coerce("The idea {conceptually} maps to {\"answer\": 42} overall.");
        TestSupport.AssertJsonEqual("""{"answer":42}""", value);
    }

    [Fact]
    public void AGenuinelyEmptyObjectStillCoerces()
    {
        TestSupport.AssertJsonEqual("{}", Llm.Coerce("{}"));
        TestSupport.AssertJsonEqual("{}", Llm.Coerce("the result was {} this time"));
    }

    [Fact]
    public void CoerceFailureMessageKeepsTheCandidateDiagnostic()
    {
        LlmJsonException failure = Assert.Throws<LlmJsonException>(() => Llm.Coerce("Data {\"coords\": (4, 5)} as requested."));
        Assert.NotNull(failure.InnerException);
        Assert.Contains("tuple", failure.Message, StringComparison.OrdinalIgnoreCase);
    }
}
