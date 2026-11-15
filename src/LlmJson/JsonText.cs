using System.Text.Json;

namespace LlmJson;

internal static class JsonText
{
    internal static JsonElement ParseElement(string json, int maxDepth)
    {
        try
        {
            using var document = JsonDocument.Parse(json, DocumentOptions(maxDepth));
            return document.RootElement.Clone();
        }
        catch (JsonException failure) when (failure is not LlmJsonException)
        {
            throw new LlmJsonException("The completed text could not be parsed as JSON.", failure);
        }
        catch (ArgumentException failure)
        {
            throw new LlmJsonException("The text is not well-formed UTF-16 and cannot be parsed as JSON.", failure);
        }
    }

    internal static bool IsValid(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return true;
        }
        catch (Exception failure) when (failure is JsonException or ArgumentException)
        {
            return false;
        }
    }

    private static JsonDocumentOptions DocumentOptions(int maxDepth)
    {
        return new JsonDocumentOptions { MaxDepth = Math.Max(maxDepth, PartialOptions.DefaultMaxDepth) };
    }
}
