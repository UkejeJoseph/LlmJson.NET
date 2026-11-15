using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LlmJson.Tests;

internal static class TestSupport
{
    internal static void InCulture(string cultureName, Action body)
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;
        var culture = new CultureInfo(cultureName);
        try
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
            body();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    internal static void AssertJsonEqual(string expectedJson, JsonElement actual)
    {
        JsonNode? expectedNode = JsonNode.Parse(expectedJson);
        JsonNode? actualNode = JsonNode.Parse(actual.GetRawText());
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actualNode),
            $"Expected JSON {expectedJson} but got {actual.GetRawText()}");
    }

    internal static bool IsSnapshotOf(JsonElement snapshot, JsonElement full)
    {
        if (snapshot.ValueKind != full.ValueKind)
        {
            return false;
        }

        switch (snapshot.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (JsonProperty property in snapshot.EnumerateObject())
                {
                    if (!full.TryGetProperty(property.Name, out JsonElement fullValue) || !IsSnapshotOf(property.Value, fullValue))
                    {
                        return false;
                    }
                }

                return true;
            case JsonValueKind.Array:
                if (snapshot.GetArrayLength() > full.GetArrayLength())
                {
                    return false;
                }

                int index = 0;
                foreach (JsonElement item in snapshot.EnumerateArray())
                {
                    if (!IsSnapshotOf(item, full[index]))
                    {
                        return false;
                    }

                    index++;
                }

                return true;
            case JsonValueKind.String:
                return full.GetString()!.StartsWith(snapshot.GetString()!, StringComparison.Ordinal);
            case JsonValueKind.Number:
                string snapshotRaw = snapshot.GetRawText();
                string fullRaw = full.GetRawText();
                return fullRaw.StartsWith(snapshotRaw, StringComparison.Ordinal);
            default:
                return true;
        }
    }

    internal static async IAsyncEnumerable<string> ChunksOf(string text, int chunkSize, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < text.Length; i += chunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return text.Substring(i, Math.Min(chunkSize, text.Length - i));
            await Task.Yield();
        }
    }

    internal static async IAsyncEnumerable<string> ChunkSequence(IEnumerable<string> chunks, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (string chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    internal static async Task<List<T>> CollectAsync<T>(IAsyncEnumerable<T> source)
    {
        var results = new List<T>();
        await foreach (T item in source)
        {
            results.Add(item);
        }

        return results;
    }
}
