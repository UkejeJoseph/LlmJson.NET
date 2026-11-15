using System.Text.Json;

namespace LlmJson.Tests.Partial;

public class MaxDepthForwardingTests
{
    private const int DeepLevels = 70;
    private const int RaisedDepth = 100;
    private const int ShallowDepth = 10;

    private static string DeepObject(int levels)
    {
        return string.Concat(Enumerable.Repeat("{\"a\":", levels)) + "1" + new string('}', levels);
    }

    private static int MeasureDepth(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => 1 + element.EnumerateObject().Select(p => MeasureDepth(p.Value)).DefaultIfEmpty(0).Max(),
            JsonValueKind.Array => 1 + element.EnumerateArray().Select(MeasureDepth).DefaultIfEmpty(0).Max(),
            _ => 0,
        };
    }

    [Fact]
    public void ParseAcceptsDeepStructureUnderRaisedMaxDepth()
    {
        JsonElement snapshot = PartialJson.Parse(DeepObject(DeepLevels), new PartialOptions { MaxDepth = RaisedDepth });
        Assert.Equal(DeepLevels, MeasureDepth(snapshot));
    }

    [Fact]
    public void DeserializeBindsDeepStructureUnderRaisedMaxDepth()
    {
        JsonElement value = PartialJson.Deserialize<JsonElement>(DeepObject(DeepLevels), null, new PartialOptions { MaxDepth = RaisedDepth });
        Assert.Equal(DeepLevels, MeasureDepth(value));
    }

    [Fact]
    public void DeserializeHonorsExplicitShallowSerializerMaxDepth()
    {
        JsonSerializerOptions shallow = new() { MaxDepth = ShallowDepth };
        Assert.ThrowsAny<JsonException>(() =>
            PartialJson.Deserialize<JsonElement>(DeepObject(DeepLevels), shallow, new PartialOptions { MaxDepth = RaisedDepth }));
    }

    [Fact]
    public void DeserializeDefaultDepthStillRejectsOverlyDeepStructure()
    {
        Assert.ThrowsAny<JsonException>(() => PartialJson.Deserialize<JsonElement>(DeepObject(DeepLevels)));
    }

    [Fact]
    public void CoerceToBindsDeepStructureWithoutThrowing()
    {
        JsonElement value = Llm.CoerceTo<JsonElement>(DeepObject(DeepLevels));
        Assert.Equal(JsonValueKind.Object, value.ValueKind);
    }

    [Fact]
    public void CoerceToHonorsExplicitShallowSerializerMaxDepth()
    {
        JsonSerializerOptions shallow = new() { MaxDepth = ShallowDepth };
        Assert.ThrowsAny<JsonException>(() => Llm.CoerceTo<JsonElement>(DeepObject(DeepLevels), shallow));
    }

    [Fact]
    public void DeserializeShallowStructureIsUnaffectedByDefaultDepth()
    {
        int[]? value = PartialJson.Deserialize<int[]>("[1,2,3]");
        Assert.Equal([1, 2, 3], value);
    }
}
