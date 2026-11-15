namespace LlmJson;

/// <summary>Options for parsing truncated JSON with <see cref="PartialJson"/> and <see cref="JsonStream"/>.</summary>
public sealed class PartialOptions
{
    private const int MinDepth = 1;
    private const int MaxConfigurableDepth = 1_000_000;
    internal const int DefaultMaxDepth = 64;

    private readonly int _maxDepth = DefaultMaxDepth;

    /// <summary>The default options: <see cref="LlmJson.Allow.All"/> and a nesting depth limit of 64.</summary>
    public static PartialOptions Default { get; } = new();

    /// <summary>Which incomplete value types are completed rather than omitted. Defaults to <see cref="LlmJson.Allow.All"/>.</summary>
    public Allow Allow { get; init; } = Allow.All;

    /// <summary>
    /// Maximum nesting depth accepted before parsing is rejected with <see cref="LlmJsonException"/>.
    /// Defaults to 64, the <see cref="System.Text.Json"/> default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">When the value is outside 1 to 1,000,000.</exception>
    public int MaxDepth
    {
        get => _maxDepth;
        init
        {
            if (value is < MinDepth or > MaxConfigurableDepth)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, $"MaxDepth must be between {MinDepth} and {MaxConfigurableDepth}.");
            }

            _maxDepth = value;
        }
    }
}
