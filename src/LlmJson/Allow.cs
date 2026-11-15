namespace LlmJson;

/// <summary>
/// Controls which JSON value types may be completed when they are cut off at the
/// end of a truncated document. A type that is not allowed is omitted from the
/// snapshot instead of being completed, mirroring the npm partial-json flags.
/// </summary>
[Flags]
public enum Allow
{
    /// <summary>No incomplete value is completed; only fully received values appear in the snapshot.</summary>
    None = 0,

    /// <summary>An unterminated string is closed and kept, truncated at the last safely decodable character.</summary>
    Strings = 1,

    /// <summary>A number cut off at the end of input is kept, trimming a dangling exponent when needed.</summary>
    Numbers = 2,

    /// <summary>An unclosed array is closed and kept with the elements received so far.</summary>
    Arrays = 4,

    /// <summary>An unclosed object is closed and kept with the members received so far.</summary>
    Objects = 8,

    /// <summary>A partial <c>true</c> or <c>false</c> literal is completed.</summary>
    Booleans = 16,

    /// <summary>A partial <c>null</c> literal is completed.</summary>
    Null = 32,

    /// <summary>Every incomplete value type is completed. This is the default.</summary>
    All = Strings | Numbers | Arrays | Objects | Booleans | Null,
}
