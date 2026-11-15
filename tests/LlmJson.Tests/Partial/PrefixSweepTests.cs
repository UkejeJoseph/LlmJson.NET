using System.Text.Json;

namespace LlmJson.Tests.Partial;

public class PrefixSweepTests
{
    internal const string CanonicalDocument =
        """{"model":"gpt-x","score":12.5,"active":true,"legacy":false,"note":null,"usage":{"input":152,"output":48,"details":{"cached":true,"ratio":7.5e-1}},"items":[{"id":1,"tags":["a","b"]},{"id":2,"tags":[]}],"text":"he said \"hi\" é 😀"}""";

    public static TheoryData<int> PrefixLengths()
    {
        var data = new TheoryData<int>();
        for (int length = 1; length <= CanonicalDocument.Length; length++)
        {
            data.Add(length);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(PrefixLengths))]
    public void EveryPrefixParsesUnderAllowAll(int length)
    {
        JsonElement full = JsonDocument.Parse(CanonicalDocument).RootElement;
        JsonElement snapshot = PartialJson.Parse(CanonicalDocument[..length]);
        Assert.True(TestSupport.IsSnapshotOf(snapshot, full), $"Prefix of length {length} produced {snapshot.GetRawText()}");
    }

    [Theory]
    [MemberData(nameof(PrefixLengths))]
    public void EveryPrefixParsesWithScalarsDisallowed(int length)
    {
        var options = new PartialOptions { Allow = Allow.Objects | Allow.Arrays };
        JsonElement full = JsonDocument.Parse(CanonicalDocument).RootElement;
        JsonElement snapshot = PartialJson.Parse(CanonicalDocument[..length], options);
        Assert.True(TestSupport.IsSnapshotOf(snapshot, full), $"Prefix of length {length} produced {snapshot.GetRawText()}");
    }

    [Fact]
    public void TheFullDocumentRoundTripsExactly()
    {
        TestSupport.AssertJsonEqual(CanonicalDocument, PartialJson.Parse(CanonicalDocument));
    }
}
