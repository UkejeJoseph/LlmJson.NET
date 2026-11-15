using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace LlmJson;

/// <summary>
/// Turns a raw LLM token stream into a stream of parsed snapshots. The input is a
/// plain <see cref="IAsyncEnumerable{T}"/> of string deltas, so it works with any
/// SDK: OpenAI, Anthropic, Microsoft.Extensions.AI, or a hand-rolled SSE reader.
/// </summary>
public static class JsonStream
{
    /// <summary>
    /// Accumulates chunks and yields a parsed snapshot whenever new content changes it.
    /// Chunks that do not change the snapshot (whitespace, a comma, half an escape
    /// sequence) are skipped, so consumers only see distinct states.
    /// Each chunk re-parses the entire accumulated buffer, so total cost grows
    /// quadratically with stream length and the buffer is unbounded; for very long
    /// generations, debounce deltas by time before feeding them in.
    /// A stream whose accumulated text becomes invalid mid-flight simply stops yielding
    /// new snapshots without a terminal error; validating the final state is the caller's job.
    /// </summary>
    /// <param name="chunks">The raw text deltas of a JSON generation, in order.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels enumeration of the source and snapshot production.</param>
    /// <returns>Distinct snapshots, each a <see cref="JsonElement"/> that outlives the enumeration step.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="chunks"/> is null.</exception>
    public static IAsyncEnumerable<JsonElement> Snapshots(IAsyncEnumerable<string> chunks, PartialOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return Core(chunks, options ?? PartialOptions.Default, cancellationToken);

        static async IAsyncEnumerable<JsonElement> Core(IAsyncEnumerable<string> chunks, PartialOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (string completed in CompletedTexts(chunks, options, cancellationToken).ConfigureAwait(false))
            {
                yield return JsonText.ParseElement(completed, options.MaxDepth);
            }
        }
    }

    /// <summary>
    /// Accumulates chunks and yields a live typed view of the JSON being generated:
    /// properties fill in progressively as their tokens arrive. Snapshots that do not
    /// yet bind to <typeparamref name="T"/> (for example an enum value cut mid-word)
    /// are transient and skipped.
    /// Each chunk re-parses the entire accumulated buffer, so total cost grows
    /// quadratically with stream length and the buffer is unbounded; for very long
    /// generations, debounce deltas by time before feeding them in.
    /// A stream whose accumulated text becomes invalid mid-flight simply stops yielding
    /// new snapshots without a terminal error; validating the final state is the caller's job.
    /// </summary>
    /// <typeparam name="T">The target type; properties missing from the received prefix keep their default values.</typeparam>
    /// <param name="chunks">The raw text deltas of a JSON generation, in order.</param>
    /// <param name="serializerOptions">Serializer options used to bind each snapshot to <typeparamref name="T"/>.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <param name="cancellationToken">Cancels enumeration of the source and snapshot production.</param>
    /// <returns>Progressively filled values of <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="chunks"/> is null.</exception>
    public static IAsyncEnumerable<T?> Snapshots<T>(IAsyncEnumerable<string> chunks, JsonSerializerOptions? serializerOptions = null, PartialOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        return Core(chunks, serializerOptions, options ?? PartialOptions.Default, cancellationToken);

        static async IAsyncEnumerable<T?> Core(IAsyncEnumerable<string> chunks, JsonSerializerOptions? serializerOptions, PartialOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await foreach (string completed in CompletedTexts(chunks, options, cancellationToken).ConfigureAwait(false))
            {
                T? value;
                try
                {
                    value = JsonSerializer.Deserialize<T>(completed, serializerOptions);
                }
                catch (JsonException)
                {
                    continue;
                }

                yield return value;
            }
        }
    }

    private static async IAsyncEnumerable<string> CompletedTexts(IAsyncEnumerable<string> chunks, PartialOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        string? previous = null;
        await foreach (string? chunk in chunks.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            buffer.Append(chunk);
            if (!PartialCompleter.TryComplete(buffer.ToString(), options, out string completed))
            {
                continue;
            }

            if (completed == previous)
            {
                continue;
            }

            previous = completed;
            yield return completed;
        }
    }
}
