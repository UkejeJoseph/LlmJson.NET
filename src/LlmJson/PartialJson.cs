using System.Text.Json;

namespace LlmJson;

/// <summary>
/// Parses truncated JSON, the kind an LLM produces while it is still generating,
/// by completing the received prefix into a valid snapshot. Semantics mirror the
/// npm partial-json parser: an incomplete trailing value is completed when its
/// type is allowed by <see cref="PartialOptions.Allow"/> and omitted otherwise.
/// </summary>
public static class PartialJson
{
    /// <summary>Parses a truncated JSON prefix into a valid snapshot.</summary>
    /// <param name="truncatedJson">Any prefix of a JSON document, or a complete document.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <returns>The snapshot as a <see cref="JsonElement"/> that outlives the call.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="truncatedJson"/> is null.</exception>
    /// <exception cref="LlmJsonException">When no snapshot can be produced under the active flags.</exception>
    public static JsonElement Parse(string truncatedJson, PartialOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(truncatedJson);
        PartialOptions effective = options ?? PartialOptions.Default;
        string completed = PartialCompleter.Complete(truncatedJson, effective);
        return JsonText.ParseElement(completed, effective.MaxDepth);
    }

    /// <summary>Attempts to parse a truncated JSON prefix without throwing.</summary>
    /// <param name="truncatedJson">Any prefix of a JSON document, or a complete document.</param>
    /// <param name="snapshot">The snapshot when parsing succeeded; default otherwise.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <returns>True when a snapshot was produced; false for any invalid input, including null.</returns>
    public static bool TryParse(string? truncatedJson, out JsonElement snapshot, PartialOptions? options = null)
    {
        snapshot = default;
        if (truncatedJson is null)
        {
            return false;
        }

        try
        {
            snapshot = Parse(truncatedJson, options);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Parses a truncated JSON prefix and deserializes the snapshot to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The target type; properties missing from the truncated tail keep their default values.</typeparam>
    /// <param name="truncatedJson">Any prefix of a JSON document, or a complete document.</param>
    /// <param name="serializerOptions">Serializer options used to bind the snapshot to <typeparamref name="T"/>.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <returns>The deserialized snapshot.</returns>
    /// <remarks>
    /// The serializer nesting limit is aligned with <see cref="PartialOptions.MaxDepth"/> so a structure
    /// deep enough for <see cref="Parse"/> to accept also binds here. An explicit
    /// <see cref="JsonSerializerOptions.MaxDepth"/> wins; System.Text.Json reports an unset limit as 0,
    /// which is treated as unset and filled from <see cref="PartialOptions.MaxDepth"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="truncatedJson"/> is null.</exception>
    /// <exception cref="LlmJsonException">When no snapshot can be produced under the active flags.</exception>
    /// <exception cref="JsonException">When the snapshot does not bind to <typeparamref name="T"/>.</exception>
    public static T? Deserialize<T>(string truncatedJson, JsonSerializerOptions? serializerOptions = null, PartialOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(truncatedJson);
        PartialOptions effective = options ?? PartialOptions.Default;
        string completed = PartialCompleter.Complete(truncatedJson, effective);
        return JsonSerializer.Deserialize<T>(completed, SerializerDepth.Align(serializerOptions, effective.MaxDepth));
    }

    /// <summary>Attempts to parse a truncated JSON prefix and bind the snapshot to <typeparamref name="T"/> without throwing.</summary>
    /// <typeparam name="T">The target type; properties missing from the truncated tail keep their default values.</typeparam>
    /// <param name="truncatedJson">Any prefix of a JSON document, or a complete document.</param>
    /// <param name="result">The bound snapshot when successful; default otherwise. May legitimately be null on success when the snapshot is <c>null</c>.</param>
    /// <param name="serializerOptions">Serializer options used to bind the snapshot to <typeparamref name="T"/>.</param>
    /// <param name="options">Completion options; null uses <see cref="PartialOptions.Default"/>.</param>
    /// <returns>True when a snapshot was produced and bound to <typeparamref name="T"/>; false when no snapshot can be produced under the active flags or the snapshot does not bind.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="truncatedJson"/> is null.</exception>
    public static bool TryDeserialize<T>(string truncatedJson, out T? result, JsonSerializerOptions? serializerOptions = null, PartialOptions? options = null)
    {
        try
        {
            result = Deserialize<T>(truncatedJson, serializerOptions, options);
            return true;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }
}
