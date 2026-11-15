using System.Text.Json;

namespace LlmJson.Tests.Partial;

public class DeserializeTests
{
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    private sealed record Invoice(string? Customer, decimal Total, List<string>? Items);

    [Fact]
    public void TruncatedTailYieldsDefaultsForMissingProperties()
    {
        Invoice? invoice = PartialJson.Deserialize<Invoice>("""{"customer":"Acme""", CaseInsensitive);
        Assert.NotNull(invoice);
        Assert.Equal("Acme", invoice.Customer);
        Assert.Equal(0m, invoice.Total);
        Assert.Null(invoice.Items);
    }

    [Fact]
    public void PartiallyReceivedListIsBoundWithCompletedElements()
    {
        Invoice? invoice = PartialJson.Deserialize<Invoice>("""{"customer":"Acme","total":125.5,"items":["a","b""", CaseInsensitive);
        Assert.NotNull(invoice);
        Assert.Equal(125.5m, invoice.Total);
        Assert.Equal(["a", "b"], invoice.Items);
    }

    [Fact]
    public void DisallowedPartialsAreOmittedFromTheBinding()
    {
        var options = new PartialOptions { Allow = Allow.Objects | Allow.Arrays };
        Invoice? invoice = PartialJson.Deserialize<Invoice>("""{"customer":"Acme","items":["a","b""", CaseInsensitive, options);
        Assert.NotNull(invoice);
        Assert.Equal("Acme", invoice.Customer);
        Assert.Equal(["a"], invoice.Items);
    }

    [Fact]
    public void CompleteDocumentBindsFully()
    {
        Invoice? invoice = PartialJson.Deserialize<Invoice>("""{"customer":"Acme","total":9,"items":[]}""", CaseInsensitive);
        Assert.NotNull(invoice);
        Assert.Equal(9m, invoice.Total);
        Assert.Empty(invoice.Items!);
    }

    [Fact]
    public void TryDeserializeBindsATruncatedTailWithDefaults()
    {
        Assert.True(PartialJson.TryDeserialize("""{"customer":"Acme""", out Invoice? invoice, CaseInsensitive));
        Assert.NotNull(invoice);
        Assert.Equal("Acme", invoice.Customer);
        Assert.Equal(0m, invoice.Total);
        Assert.Null(invoice.Items);
    }

    [Fact]
    public void TryDeserializeBindsAPrimitivePrefix()
    {
        Assert.True(PartialJson.TryDeserialize("125.5", out decimal total));
        Assert.Equal(125.5m, total);
    }

    [Fact]
    public void TryDeserializeReturnsFalseAndDefaultWhenTheSnapshotDoesNotBind()
    {
        Assert.False(PartialJson.TryDeserialize("""[1, 2, 3""", out Invoice? invoice, CaseInsensitive));
        Assert.Null(invoice);
    }

    [Fact]
    public void TryDeserializeReturnsFalseAndDefaultForPrimitiveTarget()
    {
        Assert.False(PartialJson.TryDeserialize("""{"a": 1""", out decimal total));
        Assert.Equal(0m, total);
    }

    [Fact]
    public void TryDeserializeThrowsOnNullArgument()
    {
        Assert.Throws<ArgumentNullException>(() => PartialJson.TryDeserialize<Invoice>(null!, out _, CaseInsensitive));
    }

    [Fact]
    public void TryDeserializeMatchesDeserializeWhenItSucceeds()
    {
        const string prefix = """{"customer":"Acme","total":125.5,"items":["a","b""";
        Assert.True(PartialJson.TryDeserialize(prefix, out Invoice? tried, CaseInsensitive));
        Invoice? thrown = PartialJson.Deserialize<Invoice>(prefix, CaseInsensitive);
        Assert.Equal(JsonSerializer.Serialize(thrown), JsonSerializer.Serialize(tried));
    }
}
