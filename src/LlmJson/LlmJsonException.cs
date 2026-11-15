using System.Text.Json;

namespace LlmJson;

/// <summary>
/// Thrown when input cannot be turned into valid JSON: the truncated prefix has no
/// completable snapshot under the active <see cref="Allow"/> flags, the text is
/// malformed beyond repair, or no JSON value could be found in the input at all.
/// </summary>
public sealed class LlmJsonException : JsonException
{
    /// <summary>Initializes the exception with a message describing the failure.</summary>
    /// <param name="message">Description of why the input could not be parsed, completed, or repaired.</param>
    public LlmJsonException(string message) : base(message)
    {
    }

    /// <summary>Initializes the exception with a message and the underlying failure.</summary>
    /// <param name="message">Description of why the input could not be parsed, completed, or repaired.</param>
    /// <param name="innerException">The original exception raised while parsing.</param>
    public LlmJsonException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
