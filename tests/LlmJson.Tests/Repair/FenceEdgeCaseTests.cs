using System.Text.Json;

namespace LlmJson.Tests.Repair;

public class FenceEdgeCaseTests
{
    [Fact]
    public void BackticksInsideAStringValueSurviveCoercion()
    {
        JsonElement value = Llm.Coerce("Here:\n```json\n{\"code\": \"```\"}\n```");
        TestSupport.AssertJsonEqual("""{"code":"```"}""", value);
    }

    [Fact]
    public void InlineFenceMarkerInsideAFencedDocumentPreservesAllMembers()
    {
        string dirty = "```json\n{\"docs\": \"wrap examples in ```json fences\", \"n\": 1}\n```";
        Assert.True(JsonRepair.TryRepair(dirty, out string? repaired));
        JsonElement element = JsonDocument.Parse(repaired!).RootElement;
        Assert.Equal("wrap examples in ```json fences", element.GetProperty("docs").GetString());
        Assert.Equal(1, element.GetProperty("n").GetInt32());
    }

    [Fact]
    public void MidLineBackticksInsideSingleQuotedStringsRepair()
    {
        Assert.Equal("""{"doc":"use ``` to fence","n":1}""", JsonRepair.Repair("{'doc': 'use ``` to fence', 'n': 1}"));
    }

    [Fact]
    public void TruncatedInputWithBacktickStringCompletesSanely()
    {
        Assert.Equal("""{"code":"```","n":1}""", JsonRepair.Repair("{\"code\": \"```\", \"n\": 1"));
    }

    [Fact]
    public void LineStartFencesStillStripNormally()
    {
        Assert.Equal("{\"a\": 1}", JsonRepair.Repair("```json\n{\"a\": 1}\n```"));
        Assert.Equal("[1,2]", JsonRepair.Repair("intro text\n```\n[1,2]\n```\noutro"));
    }
}
