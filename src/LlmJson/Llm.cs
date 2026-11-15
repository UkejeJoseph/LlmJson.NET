using System.Text.Json;

namespace LlmJson;

/// <summary>
/// One-call coercion of any LLM output into JSON: extract the value from
/// surrounding prose, repair it, complete a truncated tail, and parse.
/// </summary>
public static class Llm
{
    /// <summary>
    /// Finds the first JSON value in surrounding text: prose before or after, inside a
    /// markdown code fence or not. A balanced object or array is returned exactly as it
    /// appears; when the text ends before the value closes (a generation cut off
    /// mid-stream), the unterminated tail is returned so the partial engine can complete it.
    /// </summary>
    /// <param name="prose">The text to search.</param>
    /// <returns>The extracted JSON candidate, or null when the text contains no object or array start.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="prose"/> is null.</exception>
    public static string? ExtractJson(string prose)
    {
        ArgumentNullException.ThrowIfNull(prose);
        foreach (JsonCandidate candidate in JsonExtractor.Candidates(prose))
        {
            return candidate.Text;
        }

        return null;
    }

    /// <summary>Coerces arbitrary LLM output into a parsed JSON value.</summary>
    /// <param name="anything">Raw model output: prose, fenced blocks, dirty JSON, truncated JSON, or any mix.</param>
    /// <returns>The first coercible JSON value as a <see cref="JsonElement"/> that outlives the call.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="anything"/> is null.</exception>
    /// <exception cref="LlmJsonException">When no coercible JSON value is found.</exception>
    public static JsonElement Coerce(string anything)
    {
        ArgumentNullException.ThrowIfNull(anything);
        return JsonText.ParseElement(CoerceText(anything), PartialOptions.DefaultMaxDepth);
    }

    /// <summary>Attempts to coerce arbitrary LLM output into a parsed JSON value without throwing.</summary>
    /// <param name="anything">Raw model output.</param>
    /// <param name="value">The coerced value when successful; default otherwise.</param>
    /// <returns>True when a JSON value was coerced; false for any uncoercible input, including null.</returns>
    public static bool TryCoerce(string? anything, out JsonElement value)
    {
        value = default;
        if (anything is null)
        {
            return false;
        }

        try
        {
            value = Coerce(anything);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Coerces arbitrary LLM output and deserializes the result to <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">The target type; properties missing from a truncated tail keep their default values.</typeparam>
    /// <param name="anything">Raw model output: prose, fenced blocks, dirty JSON, truncated JSON, or any mix.</param>
    /// <param name="serializerOptions">Serializer options used to bind the coerced JSON to <typeparamref name="T"/>.</param>
    /// <returns>The deserialized value.</returns>
    /// <remarks>
    /// The serializer nesting limit is aligned with <see cref="PartialOptions.Default"/>'s depth so it can
    /// bind whatever coercion produced. An explicit <see cref="JsonSerializerOptions.MaxDepth"/> wins;
    /// System.Text.Json reports an unset limit as 0, which is treated as unset and filled from the default depth.
    /// </remarks>
    /// <exception cref="ArgumentNullException">When <paramref name="anything"/> is null.</exception>
    /// <exception cref="LlmJsonException">When no coercible JSON value is found.</exception>
    /// <exception cref="JsonException">When the coerced JSON does not bind to <typeparamref name="T"/>.</exception>
    public static T? CoerceTo<T>(string anything, JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(anything);
        return JsonSerializer.Deserialize<T>(CoerceText(anything), SerializerDepth.Align(serializerOptions, PartialOptions.DefaultMaxDepth));
    }

    /// <summary>Attempts to coerce arbitrary LLM output and bind the result to <typeparamref name="T"/> without throwing.</summary>
    /// <typeparam name="T">The target type; properties missing from a truncated tail keep their default values.</typeparam>
    /// <param name="anything">Raw model output: prose, fenced blocks, dirty JSON, truncated JSON, or any mix.</param>
    /// <param name="result">The bound value when successful; default otherwise. May legitimately be null on success when the coerced JSON is <c>null</c>.</param>
    /// <param name="serializerOptions">Serializer options used to bind the coerced JSON to <typeparamref name="T"/>.</param>
    /// <returns>True when a JSON value was coerced and bound to <typeparamref name="T"/>; false when no coercible JSON value is found or the coerced JSON does not bind.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="anything"/> is null.</exception>
    public static bool TryCoerceTo<T>(string anything, out T? result, JsonSerializerOptions? serializerOptions = null)
    {
        try
        {
            result = CoerceTo<T>(anything, serializerOptions);
            return true;
        }
        catch (JsonException)
        {
            result = default;
            return false;
        }
    }

    private static string CoerceText(string anything)
    {
        LlmJsonException? lastFailure = null;
        string? firstNonEmpty = null;
        string? firstAny = null;
        foreach (JsonCandidate candidate in JsonExtractor.Candidates(anything))
        {
            string repaired;
            try
            {
                repaired = JsonRepair.Repair(candidate.Text);
            }
            catch (LlmJsonException failure)
            {
                lastFailure = failure;
                continue;
            }

            bool empty = IsEmptyContainer(repaired);
            if (candidate.IsBalanced && !empty)
            {
                return repaired;
            }

            if (!empty)
            {
                firstNonEmpty ??= repaired;
            }

            firstAny ??= repaired;
        }

        if (firstNonEmpty is not null)
        {
            return firstNonEmpty;
        }

        if (firstAny is not null)
        {
            return firstAny;
        }

        try
        {
            return JsonRepair.Repair(anything);
        }
        catch (LlmJsonException fallbackFailure)
        {
            string message = lastFailure is null
                ? "No coercible JSON value was found in the input."
                : $"No coercible JSON value was found in the input. Last candidate failure: {lastFailure.Message}";
            throw new LlmJsonException(message, fallbackFailure);
        }
    }

    private static bool IsEmptyContainer(string json)
    {
        return json.Trim() is "{}" or "[]";
    }
}
