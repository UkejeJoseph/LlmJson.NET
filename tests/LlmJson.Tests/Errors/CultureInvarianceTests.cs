using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmJson.Tests.Errors;

public class CultureInvarianceTests
{
    private enum Status
    {
        Inactive,
        Active,
    }

    private sealed record Reading(double Amount, decimal Price, double Ratio, DateTime When, Status Status, string? Title, long Id);

    private static readonly JsonSerializerOptions Options = BuildOptions();

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private static readonly DateTime ExpectedWhen = new(2026, 8, 7, 13, 45, 30, DateTimeKind.Utc);

    private const string TruncatedExponentTail =
        """{"amount":1234.56,"price":19.99,"when":"2026-08-07T13:45:30Z","status":"ACTIVE","title":"Widget","id":42,"ratio":1.25e""";

    private static void AssertReading(Reading? reading)
    {
        Assert.NotNull(reading);
        Assert.Equal(1234.56, reading.Amount);
        Assert.Equal(19.99m, reading.Price);
        Assert.Equal(1.25, reading.Ratio);
        Assert.Equal(ExpectedWhen, reading.When.ToUniversalTime());
        Assert.Equal(Status.Active, reading.Status);
        Assert.Equal("Widget", reading.Title);
        Assert.Equal(42L, reading.Id);
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void DeserializeIsCultureInvariant(string cultureName)
    {
        TestSupport.InCulture(cultureName, () =>
        {
            Reading? reading = PartialJson.Deserialize<Reading>(TruncatedExponentTail, Options);
            AssertReading(reading);
        });
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    [InlineData("en-US")]
    public void CoerceToIsCultureInvariant(string cultureName)
    {
        string prose = "Sure, here is the reading:\n```json\n" + TruncatedExponentTail;
        TestSupport.InCulture(cultureName, () =>
        {
            Reading? reading = Llm.CoerceTo<Reading>(prose, Options);
            AssertReading(reading);
        });
    }

    [Theory]
    [InlineData("tr-TR")]
    [InlineData("de-DE")]
    public void CaseInsensitiveEnumHasNoTurkishIBug(string cultureName)
    {
        TestSupport.InCulture(cultureName, () =>
        {
            Reading? reading = PartialJson.Deserialize<Reading>("""{"status":"ACTIVE","id":1}""", Options);
            Assert.NotNull(reading);
            Assert.Equal(Status.Active, reading.Status);
            Assert.Equal(1L, reading.Id);
        });
    }
}
