using System.Text;

namespace LlmJson;

internal static class RepairEngine
{
    private const string NoValueMessage = "The input does not contain a repairable JSON value.";
    private const string TooDeepMessage = "Nesting depth exceeds the repair limit of 64.";
    private const string TupleMessage = "Python tuple or set syntax cannot be repaired to JSON without guessing; rejecting instead.";
    private const string Fence = "```";
    private const string ValueDelimiters = ",:{}[]\"'“”‘’";
    private const string BareWordTerminators = ",:{}[]\"'“”‘’\r\n";

    private enum State
    {
        ExpectValue,
        AfterValue,
        ExpectKey,
        AfterKey,
    }

    private sealed class Frame
    {
        public bool IsObject;
        public int CommittedLength;
        public bool HasCommitted;
    }

    internal static string StripFence(string text)
    {
        int open = IndexOfLineStartFence(text, 0);
        if (open < 0)
        {
            return text;
        }

        int contentStart = open + Fence.Length;
        while (contentStart < text.Length && char.IsAsciiLetter(text[contentStart]))
        {
            contentStart++;
        }

        if (contentStart < text.Length && text[contentStart] == '\r')
        {
            contentStart++;
        }

        if (contentStart < text.Length && text[contentStart] == '\n')
        {
            contentStart++;
        }

        int close = IndexOfLineStartFence(text, contentStart);
        return close < 0 ? text[contentStart..] : text[contentStart..close];
    }

    private static int IndexOfLineStartFence(string text, int from)
    {
        int i = from;
        while (i <= text.Length - Fence.Length)
        {
            int found = text.IndexOf(Fence, i, StringComparison.Ordinal);
            if (found < 0)
            {
                return -1;
            }

            if (found == 0 || text[found - 1] == '\n')
            {
                return found;
            }

            i = found + 1;
        }

        return -1;
    }

    internal static string Transform(string text)
    {
        var values = new List<string>();
        int i = 0;
        while (true)
        {
            SkipInsignificant(text, ref i);
            if (i >= text.Length)
            {
                break;
            }

            var output = new StringBuilder();
            if (values.Count == 0)
            {
                TransformValue(text, ref i, output);
                if (output.Length > 0)
                {
                    values.Add(output.ToString());
                }
            }
            else
            {
                try
                {
                    TransformValue(text, ref i, output);
                }
                catch (LlmJsonException)
                {
                    break;
                }

                if (output.Length == 0)
                {
                    break;
                }

                values.Add(output.ToString());
            }
        }

        if (values.Count == 0)
        {
            throw new LlmJsonException(NoValueMessage);
        }

        return values.Count == 1 ? values[0] : "[" + string.Join(",", values) + "]";
    }

    private static void TransformValue(string s, ref int i, StringBuilder output)
    {
        int n = s.Length;
        var stack = new List<Frame>();
        State state = State.ExpectValue;

        while (true)
        {
            SkipInsignificant(s, ref i);
            if (i >= n)
            {
                return;
            }

            char c = s[i];
            Frame? frame = stack.Count > 0 ? stack[^1] : null;
            switch (state)
            {
                case State.ExpectValue:
                    if (IsQuote(c))
                    {
                        AppendValueSeparator(output, frame);
                        ScanFlexString(s, ref i, output);
                        if (Commit(output, stack, ref state))
                        {
                            return;
                        }

                        break;
                    }

                    if (c == '{' || c == '[')
                    {
                        if (stack.Count >= PartialOptions.DefaultMaxDepth)
                        {
                            throw new LlmJsonException(TooDeepMessage);
                        }

                        AppendValueSeparator(output, frame);
                        output.Append(c);
                        stack.Add(new Frame { IsObject = c == '{', CommittedLength = output.Length });
                        state = c == '{' ? State.ExpectKey : State.ExpectValue;
                        i++;
                        break;
                    }

                    if (c is '}' or ']')
                    {
                        i++;
                        if (frame is null)
                        {
                            break;
                        }

                        if (Close(output, stack, ref state))
                        {
                            return;
                        }

                        break;
                    }

                    if (c == ',')
                    {
                        i++;
                        break;
                    }

                    if (c == '(')
                    {
                        throw new LlmJsonException(TupleMessage);
                    }

                    if (!TransformBareValue(s, ref i, output, stack, frame, ref state, out bool rootDone))
                    {
                        throw new LlmJsonException(NoValueMessage);
                    }

                    if (rootDone)
                    {
                        return;
                    }

                    break;

                case State.AfterValue:
                    if (c == ',')
                    {
                        state = frame!.IsObject ? State.ExpectKey : State.ExpectValue;
                        i++;
                        break;
                    }

                    if (c is '}' or ']')
                    {
                        i++;
                        if (Close(output, stack, ref state))
                        {
                            return;
                        }

                        break;
                    }

                    if (c == ':')
                    {
                        i++;
                        break;
                    }

                    state = frame!.IsObject ? State.ExpectKey : State.ExpectValue;
                    break;

                case State.ExpectKey:
                    if (c is '}' or ']')
                    {
                        i++;
                        if (Close(output, stack, ref state))
                        {
                            return;
                        }

                        break;
                    }

                    if (c == ',')
                    {
                        i++;
                        break;
                    }

                    if (frame!.HasCommitted)
                    {
                        output.Append(',');
                    }

                    if (IsQuote(c))
                    {
                        ScanFlexString(s, ref i, output);
                    }
                    else
                    {
                        AppendBareKey(s, ref i, output);
                    }

                    state = State.AfterKey;
                    break;

                case State.AfterKey:
                    output.Append(':');
                    state = State.ExpectValue;
                    if (c == ':' || c == '=')
                    {
                        i++;
                    }

                    break;

                default:
                    throw new LlmJsonException("Unreachable repair state.");
            }
        }
    }

    private static bool TransformBareValue(string s, ref int i, StringBuilder output, List<Frame> stack, Frame? frame, ref State state, out bool rootDone)
    {
        rootDone = false;
        int start = i;
        while (i < s.Length && !char.IsWhiteSpace(s[i]) && ValueDelimiters.IndexOf(s[i]) < 0 && !IsCommentStart(s, i))
        {
            i++;
        }

        if (i == start)
        {
            i++;
            return frame is not null;
        }

        string token = s[start..i];
        if (TryMapLiteral(token, out string? mapped))
        {
            AppendValueSeparator(output, frame);
            output.Append(mapped);
            rootDone = Commit(output, stack, ref state);
            return true;
        }

        if (IsEllipsisToken(token))
        {
            if (frame is not null)
            {
                output.Length = frame.CommittedLength;
                state = frame.IsObject ? State.ExpectKey : State.ExpectValue;
            }

            return true;
        }

        if (TryNormalizeNumber(token, out string? number))
        {
            AppendValueSeparator(output, frame);
            output.Append(number);
            rootDone = Commit(output, stack, ref state);
            return true;
        }

        if (frame is null)
        {
            return false;
        }

        while (i < s.Length && BareWordTerminators.IndexOf(s[i]) < 0 && !IsCommentStart(s, i))
        {
            i++;
        }

        string bare = s[start..i].TrimEnd();
        AppendValueSeparator(output, frame);
        AppendJsonString(output, bare);
        rootDone = Commit(output, stack, ref state);
        return true;
    }

    private static void AppendBareKey(string s, ref int i, StringBuilder output)
    {
        int start = i;
        while (i < s.Length && BareWordTerminators.IndexOf(s[i]) < 0 && !IsCommentStart(s, i))
        {
            i++;
        }

        AppendJsonString(output, s[start..i].TrimEnd());
    }

    private static bool TryMapLiteral(string token, out string? mapped)
    {
        mapped = token switch
        {
            "true" or "True" or "TRUE" => "true",
            "false" or "False" or "FALSE" => "false",
            "null" or "Null" or "NULL" or "None" or "NONE" => "null",
            "undefined" or "NaN" or "Infinity" or "-Infinity" or "+Infinity" => "null",
            _ => null,
        };
        return mapped is not null;
    }

    private static bool IsEllipsisToken(string token)
    {
        if (token == "…")
        {
            return true;
        }

        if (token.Length == 0)
        {
            return false;
        }

        foreach (char c in token)
        {
            if (c != '.')
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryNormalizeNumber(string token, out string? number)
    {
        number = null;
        ReadOnlySpan<char> span = token.AsSpan();
        if (span.Length > 0 && span[0] == '+')
        {
            span = span[1..];
        }

        bool negative = span.Length > 0 && span[0] == '-';
        ReadOnlySpan<char> digits = negative ? span[1..] : span;
        var builder = new StringBuilder(span.Length + 2);
        if (negative)
        {
            builder.Append('-');
        }

        if (digits.StartsWith("."))
        {
            builder.Append('0').Append(digits);
        }
        else if (digits.EndsWith("."))
        {
            builder.Append(digits[..^1]);
        }
        else
        {
            builder.Append(digits);
        }

        string candidate = builder.ToString();
        if (PartialCompleter.IsValidNumber(candidate))
        {
            number = candidate;
            return true;
        }

        if (PartialCompleter.TryTrimNumberTail(candidate, out ReadOnlySpan<char> trimmed))
        {
            number = trimmed.ToString();
            return true;
        }

        return false;
    }

    private static void ScanFlexString(string s, ref int i, StringBuilder output)
    {
        char open = s[i];
        bool single = open is '\'' or '‘' or '’';
        bool strictDouble = open == '"';
        i++;
        output.Append('"');
        while (i < s.Length)
        {
            char c = s[i];
            if (strictDouble ? c == '"' : IsClosingQuote(c, single) && IsRealCloseCandidate(s, i))
            {
                i++;
                output.Append('"');
                return;
            }

            if (c == '\\')
            {
                if (i + 1 >= s.Length)
                {
                    i++;
                    continue;
                }

                char escape = s[i + 1];
                if (escape == 'u' && i + 6 > s.Length)
                {
                    i = s.Length;
                    continue;
                }

                if (escape == 'u' && IsHex4(s, i + 2))
                {
                    output.Append(s, i, 6);
                    i += 6;
                    continue;
                }

                if ("\"\\/bfnrt".IndexOf(escape) >= 0)
                {
                    output.Append(s, i, 2);
                    i += 2;
                    continue;
                }

                i++;
                AppendContentChar(s, ref i, output);
                continue;
            }

            AppendContentChar(s, ref i, output);
        }

        output.Append('"');
    }

    private static void AppendContentChar(string s, ref int i, StringBuilder output)
    {
        char c = s[i];
        if (char.IsHighSurrogate(c))
        {
            if (i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
            {
                output.Append(c).Append(s[i + 1]);
                i += 2;
                return;
            }

            i++;
            if (i < s.Length)
            {
                output.Append('�');
            }

            return;
        }

        if (char.IsLowSurrogate(c))
        {
            output.Append('�');
            i++;
            return;
        }

        AppendEscapedChar(output, c);
        i++;
    }

    private static bool IsClosingQuote(char c, bool single)
    {
        return single ? c is '\'' or '‘' or '’' : c is '"' or '“' or '”';
    }

    private static bool IsRealCloseCandidate(string s, int index)
    {
        int next = index + 1;
        while (next < s.Length && (s[next] == ' ' || s[next] == '\t'))
        {
            next++;
        }

        return next >= s.Length || s[next] is ',' or ':' or ']' or '}' or '\r' or '\n' or '"' or '\'' or '“' or '”' or '‘' or '’';
    }

    private static void AppendEscapedChar(StringBuilder output, char c)
    {
        switch (c)
        {
            case '"':
                output.Append("\\\"");
                return;
            case '\\':
                output.Append("\\\\");
                return;
            case '\n':
                output.Append("\\n");
                return;
            case '\r':
                output.Append("\\r");
                return;
            case '\t':
                output.Append("\\t");
                return;
            default:
                if (c < 0x20 || char.IsSurrogate(c))
                {
                    output.Append("\\u").Append(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                    return;
                }

                output.Append(c);
                return;
        }
    }

    private static void SkipInsignificant(string s, ref int i)
    {
        while (i < s.Length)
        {
            char c = s[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '/')
            {
                while (i < s.Length && s[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? s.Length : end + 2;
                continue;
            }

            return;
        }
    }

    private static bool IsCommentStart(string s, int index)
    {
        return s[index] == '/' && index + 1 < s.Length && (s[index + 1] == '/' || s[index + 1] == '*');
    }

    private static bool IsQuote(char c)
    {
        return c is '"' or '\'' or '“' or '”' or '‘' or '’';
    }

    private static bool IsHex4(string s, int start)
    {
        if (start + 4 > s.Length)
        {
            return false;
        }

        for (int i = start; i < start + 4; i++)
        {
            if (!char.IsAsciiHexDigit(s[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Commit(StringBuilder output, List<Frame> stack, ref State state)
    {
        if (stack.Count == 0)
        {
            return true;
        }

        Frame frame = stack[^1];
        frame.CommittedLength = output.Length;
        frame.HasCommitted = true;
        state = State.AfterValue;
        return false;
    }

    private static bool Close(StringBuilder output, List<Frame> stack, ref State state)
    {
        Frame frame = stack[^1];
        stack.RemoveAt(stack.Count - 1);
        output.Length = frame.CommittedLength;
        output.Append(frame.IsObject ? '}' : ']');
        return Commit(output, stack, ref state);
    }

    private static void AppendValueSeparator(StringBuilder output, Frame? frame)
    {
        if (frame is { IsObject: false, HasCommitted: true })
        {
            output.Append(',');
        }
    }

    private static void AppendJsonString(StringBuilder output, string value)
    {
        output.Append('"');
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                output.Append(c).Append(value[i + 1]);
                i += 2;
                continue;
            }

            if (char.IsSurrogate(c))
            {
                output.Append('�');
                i++;
                continue;
            }

            AppendEscapedChar(output, c);
            i++;
        }

        output.Append('"');
    }
}
