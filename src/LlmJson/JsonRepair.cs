using System.Text.Json;

namespace LlmJson;

/// <summary>
/// Repairs almost-JSON into valid JSON. Fixes the mistakes LLMs actually make:
/// markdown code fences, single-quoted and smart-quoted strings, unquoted keys,
/// trailing and missing commas, Python literals, JavaScript undefined, comments,
/// unescaped newlines inside strings, and truncation. Semantics are inspired by
/// the jsonrepair library; the implementation is an independent clean-room port.
/// </summary>
public static class JsonRepair
{
    /// <summary>
    /// Repairs a dirty JSON text into valid JSON. Input that is already valid JSON is
    /// returned unchanged; repaired output is re-encoded. Concatenated top-level values
    /// (including JSONL) are wrapped into an array. Repair depth is capped at 64 nesting
    /// levels regardless of <see cref="PartialOptions.MaxDepth"/>.
    /// </summary>
    /// <param name="dirtyJson">The almost-JSON text.</param>
    /// <returns>Valid JSON text.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="dirtyJson"/> is null.</exception>
    /// <exception cref="LlmJsonException">When the input is invalid beyond repair.</exception>
    public static string Repair(string dirtyJson)
    {
        ArgumentNullException.ThrowIfNull(dirtyJson);
        if (JsonText.IsValid(dirtyJson))
        {
            return dirtyJson;
        }

        string stripped = RepairEngine.StripFence(dirtyJson).Trim();
        if (JsonText.IsValid(stripped))
        {
            return stripped;
        }

        string transformed = RepairEngine.Transform(stripped);
        if (JsonText.IsValid(transformed))
        {
            return transformed;
        }

        string completed = PartialCompleter.Complete(transformed, PartialOptions.Default);
        if (JsonText.IsValid(completed))
        {
            return completed;
        }

        throw new LlmJsonException("The input could not be repaired into valid JSON.");
    }

    /// <summary>Attempts to repair a dirty JSON text without throwing.</summary>
    /// <param name="dirtyJson">The almost-JSON text.</param>
    /// <param name="repaired">The repaired valid JSON, or null when repair failed.</param>
    /// <returns>True when repair succeeded; false for any unrepairable input, including null.</returns>
    public static bool TryRepair(string? dirtyJson, out string? repaired)
    {
        repaired = null;
        if (dirtyJson is null)
        {
            return false;
        }

        try
        {
            repaired = Repair(dirtyJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
