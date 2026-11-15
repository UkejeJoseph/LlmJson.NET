using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LlmJson.Tests.Streaming;

public class JsonStreamTests
{
    private static readonly JsonSerializerOptions Web = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private sealed record Report(string? Title, List<int>? Values);

    private sealed record Schedule(DayOfWeek Day);

    [Fact]
    public async Task OneCharChunksYieldMonotonicallyGrowingSnapshots()
    {
        string document = Partial.PrefixSweepTests.CanonicalDocument;
        JsonElement full = JsonDocument.Parse(document).RootElement;
        List<JsonElement> snapshots = await TestSupport.CollectAsync(JsonStream.Snapshots(TestSupport.ChunksOf(document, 1)));

        Assert.True(snapshots.Count > 10);
        TestSupport.AssertJsonEqual(document, snapshots[^1]);
        for (int i = 0; i < snapshots.Count; i++)
        {
            Assert.True(TestSupport.IsSnapshotOf(snapshots[i], full), $"Snapshot {i} is not contained in the full document");
            if (i > 0)
            {
                Assert.True(TestSupport.IsSnapshotOf(snapshots[i - 1], snapshots[i]), $"Snapshot {i - 1} does not grow into snapshot {i}");
            }
        }
    }

    [Fact]
    public async Task UnchangedSnapshotsAreDeduplicated()
    {
        List<JsonElement> snapshots = await TestSupport.CollectAsync(
            JsonStream.Snapshots(TestSupport.ChunkSequence(["{\"a\"", ":", "1", "}"])));

        Assert.Equal(2, snapshots.Count);
        TestSupport.AssertJsonEqual("{}", snapshots[0]);
        TestSupport.AssertJsonEqual("""{"a":1}""", snapshots[1]);
    }

    [Fact]
    public async Task EmptyAndWhitespaceChunksAreHandled()
    {
        List<JsonElement> snapshots = await TestSupport.CollectAsync(
            JsonStream.Snapshots(TestSupport.ChunkSequence(["", "  ", "{\"a\"", "", ":1}", "\r\n"])));

        Assert.NotEmpty(snapshots);
        TestSupport.AssertJsonEqual("""{"a":1}""", snapshots[^1]);
    }

    [Fact]
    public async Task ChunkBoundarySplittingASurrogatePairNeverThrows()
    {
        string document = """{"emoji":"a😀b"}""";
        int highSurrogateIndex = document.IndexOf('\ud83d');
        var chunks = new[] { document[..(highSurrogateIndex + 1)], document[(highSurrogateIndex + 1)..] };
        List<JsonElement> snapshots = await TestSupport.CollectAsync(JsonStream.Snapshots(TestSupport.ChunkSequence(chunks)));

        TestSupport.AssertJsonEqual(document, snapshots[^1]);
    }

    [Fact]
    public async Task ChunkBoundarySplittingAUnicodeEscapeNeverThrows()
    {
        string document = "{\"emoji\":\"\\ud83d\\ude00\"}";
        List<JsonElement> snapshots = await TestSupport.CollectAsync(JsonStream.Snapshots(TestSupport.ChunksOf(document, 1)));

        Assert.Equal("😀", snapshots[^1].GetProperty("emoji").GetString());
    }

    [Fact]
    public async Task TypedSnapshotsFillPropertiesProgressively()
    {
        string document = """{"title":"Q1 report","values":[1,2,3]}""";
        List<Report?> snapshots = await TestSupport.CollectAsync(JsonStream.Snapshots<Report>(TestSupport.ChunksOf(document, 1), Web));

        Assert.True(snapshots.Count > 3);
        Assert.Contains(snapshots, report => report != null && report.Title == "Q1" && report.Values == null);
        Report? final = snapshots[^1];
        Assert.NotNull(final);
        Assert.Equal("Q1 report", final.Title);
        Assert.Equal([1, 2, 3], final.Values);
    }

    [Fact]
    public async Task TransientSnapshotsThatDoNotBindAreSkipped()
    {
        string document = """{"day":"Friday"}""";
        List<Schedule?> snapshots = await TestSupport.CollectAsync(JsonStream.Snapshots<Schedule>(TestSupport.ChunksOf(document, 1), Web));

        Assert.NotEmpty(snapshots);
        Assert.Equal(DayOfWeek.Friday, snapshots[^1]!.Day);
    }

    [Fact]
    public async Task CancellationStopsTheStream()
    {
        using var cts = new CancellationTokenSource();
        int seen = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (JsonElement _ in JsonStream.Snapshots(InfiniteChunks(cts.Token), cancellationToken: cts.Token))
            {
                if (++seen == 3)
                {
                    cts.Cancel();
                }
            }
        });

        Assert.Equal(3, seen);
    }

    [Fact]
    public async Task WhitespaceOnlyStreamYieldsNothing()
    {
        List<JsonElement> snapshots = await TestSupport.CollectAsync(
            JsonStream.Snapshots(TestSupport.ChunkSequence([" ", "\t", "\r\n"])));

        Assert.Empty(snapshots);
    }

    private static async IAsyncEnumerable<string> InfiniteChunks([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return "[";
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return "1,";
            await Task.Yield();
        }
    }
}
