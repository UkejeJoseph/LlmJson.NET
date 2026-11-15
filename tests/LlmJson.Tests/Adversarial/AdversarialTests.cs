using System.Diagnostics;
using System.Text.Json;

namespace LlmJson.Tests.Adversarial;

public class AdversarialTests
{
    private const int TenMegabytes = 10_000_000;
    private const long LinearTimeBudgetMilliseconds = 10_000;

    [Fact]
    public void TenMegabyteTruncatedStringParsesInLinearTime()
    {
        string truncated = "{\"data\":\"" + new string('a', TenMegabytes);
        var stopwatch = Stopwatch.StartNew();
        JsonElement snapshot = PartialJson.Parse(truncated);
        stopwatch.Stop();

        Assert.Equal(TenMegabytes, snapshot.GetProperty("data").GetString()!.Length);
        Assert.True(stopwatch.ElapsedMilliseconds < LinearTimeBudgetMilliseconds, $"Took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void DeepNestingIsCleanlyRejectedAtTheDefaultDepth()
    {
        string deep = new('[', 10_000);
        Assert.Throws<LlmJsonException>(() => PartialJson.Parse(deep));
    }

    [Fact]
    public void DeepNestingParsesWhenTheDepthIsRaised()
    {
        const int Depth = 10_000;
        string deep = new('[', Depth);
        JsonElement snapshot = PartialJson.Parse(deep, new PartialOptions { MaxDepth = Depth + 1 });

        int measured = 0;
        JsonElement current = snapshot;
        while (current.ValueKind == JsonValueKind.Array && current.GetArrayLength() == 1)
        {
            current = current[0];
            measured++;
        }

        Assert.Equal(Depth - 1, measured);
        Assert.Equal(JsonValueKind.Array, current.ValueKind);
    }

    [Fact]
    public void DepthJustBelowTheLimitParses()
    {
        string deep = new string('[', 64);
        Assert.True(PartialJson.TryParse(deep, out _));
        Assert.False(PartialJson.TryParse(new string('[', 65), out _));
    }

    [Fact]
    public void ManyKeysCompleteQuickly()
    {
        var builder = new System.Text.StringBuilder("{");
        for (int i = 0; i < 100_000; i++)
        {
            builder.Append($"\"k{i}\":{i},");
        }

        var stopwatch = Stopwatch.StartNew();
        JsonElement snapshot = PartialJson.Parse(builder.ToString());
        stopwatch.Stop();

        Assert.Equal(100_000, snapshot.EnumerateObject().Count());
        Assert.True(stopwatch.ElapsedMilliseconds < LinearTimeBudgetMilliseconds, $"Took {stopwatch.ElapsedMilliseconds} ms");
    }

    [Fact]
    public void MixedNewlineStylesInsideFencesRepair()
    {
        string dirty = "```json\r\n{\"a\": \"one\",\r\n\"b\": \"two\"\n}\r\n```";
        Assert.True(JsonRepair.TryRepair(dirty, out string? repaired));
        TestSupport.AssertJsonEqual("""{"a":"one","b":"two"}""", JsonDocument.Parse(repaired!).RootElement);
    }
}
