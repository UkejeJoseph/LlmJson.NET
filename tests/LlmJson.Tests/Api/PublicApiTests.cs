using System.Reflection;
using System.Text.Json;

namespace LlmJson.Tests.Api;

public class PublicApiTests
{
    [Fact]
    public void PublicSurfaceIsExactlyTheDocumentedTypes()
    {
        string[] expected =
        [
            nameof(Allow),
            nameof(Llm),
            nameof(LlmJsonException),
            nameof(JsonRepair),
            nameof(JsonStream),
            nameof(PartialJson),
            nameof(PartialOptions),
        ];
        string[] actual = typeof(PartialJson).Assembly
            .GetExportedTypes()
            .Select(type => type.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
    }

    [Fact]
    public void ExceptionIsSealedAndDerivesFromJsonException()
    {
        Assert.True(typeof(LlmJsonException).IsSealed);
        Assert.True(typeof(JsonException).IsAssignableFrom(typeof(LlmJsonException)));
    }

    [Fact]
    public void AllFlagCoversEveryValueType()
    {
        Assert.Equal(Allow.Strings | Allow.Numbers | Allow.Arrays | Allow.Objects | Allow.Booleans | Allow.Null, Allow.All);
    }

    [Fact]
    public void DefaultOptionsAllowEverythingAtDepth64()
    {
        Assert.Equal(Allow.All, PartialOptions.Default.Allow);
        Assert.Equal(64, PartialOptions.Default.MaxDepth);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1_000_001)]
    public void MaxDepthOutsideRangeIsRejected(int depth)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PartialOptions { MaxDepth = depth });
    }

    [Fact]
    public void NullArgumentsFailLoud()
    {
        Assert.Throws<ArgumentNullException>(() => PartialJson.Parse(null!));
        Assert.Throws<ArgumentNullException>(() => PartialJson.Deserialize<object>(null!));
        Assert.Throws<ArgumentNullException>(() => JsonStream.Snapshots(null!));
        Assert.Throws<ArgumentNullException>(() => JsonStream.Snapshots<object>(null!));
        Assert.Throws<ArgumentNullException>(() => JsonRepair.Repair(null!));
        Assert.Throws<ArgumentNullException>(() => Llm.ExtractJson(null!));
        Assert.Throws<ArgumentNullException>(() => Llm.Coerce(null!));
        Assert.Throws<ArgumentNullException>(() => Llm.CoerceTo<object>(null!));
    }

    [Fact]
    public void TryVariantsNeverThrowOnNull()
    {
        Assert.False(PartialJson.TryParse(null, out _));
        Assert.False(JsonRepair.TryRepair(null, out string? repaired));
        Assert.Null(repaired);
        Assert.False(Llm.TryCoerce(null, out _));
    }

    [Fact]
    public void ParsedSnapshotOutlivesTheCall()
    {
        JsonElement snapshot = PartialJson.Parse("""{"a": [1, 2""");
        GC.Collect();
        TestSupport.AssertJsonEqual("""{"a":[1,2]}""", snapshot);
    }
}
