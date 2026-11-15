using System.Text.Json;

namespace LlmJson;

internal static class SerializerDepth
{
    private const int UnsetMaxDepth = 0;

    internal static JsonSerializerOptions? Align(JsonSerializerOptions? serializerOptions, int partialMaxDepth)
    {
        if (serializerOptions is not null && serializerOptions.MaxDepth != UnsetMaxDepth)
        {
            return serializerOptions;
        }

        if (partialMaxDepth <= PartialOptions.DefaultMaxDepth)
        {
            return serializerOptions;
        }

        return serializerOptions is null
            ? new JsonSerializerOptions { MaxDepth = partialMaxDepth }
            : new JsonSerializerOptions(serializerOptions) { MaxDepth = partialMaxDepth };
    }
}
