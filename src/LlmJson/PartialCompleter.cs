using System.Text;

namespace LlmJson;

internal static class PartialCompleter
{
    private const string EmptyInputMessage = "The input is empty or whitespace; there is no JSON value to complete.";
    private static string Incomplete(Allow allow) => $"The truncated tail cannot be completed under the active Allow flags ({allow}).";

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

    private readonly ref struct StringScan
    {
        public required bool Closed { get; init; }
        public required bool Valid { get; init; }
        public required int End { get; init; }
        public required int SafeEnd { get; init; }
    }

    internal static bool TryComplete(string json, PartialOptions options, out string completed)
    {
        try
        {
            completed = Complete(json, options);
            return true;
        }
        catch (LlmJsonException)
        {
            completed = string.Empty;
            return false;
        }
    }

    internal static string Complete(string json, PartialOptions options)
    {
        string s = json.Trim();
        int n = s.Length;
        if (n == 0)
        {
            throw new LlmJsonException(EmptyInputMessage);
        }

        Allow allow = options.Allow;
        var output = new StringBuilder(Math.Min(n + 16, 4096));
        var stack = new List<Frame>();
        State state = State.ExpectValue;
        int i = 0;

        while (true)
        {
            while (i < n && IsJsonWhitespace(s[i]))
            {
                i++;
            }

            if (i >= n)
            {
                return ResolveUnwind(output, stack, allow, tailCommitted: state == State.AfterValue, Incomplete(allow));
            }

            char c = s[i];
            Frame? frame = stack.Count > 0 ? stack[^1] : null;
            switch (state)
            {
                case State.ExpectValue:
                    if (c == '"')
                    {
                        StringScan scan = ScanString(s, i);
                        if (!scan.Closed)
                        {
                            if ((allow & Allow.Strings) == 0)
                            {
                                return ResolveUnwind(output, stack, allow, tailCommitted: false, Incomplete(allow));
                            }

                            AppendValueSeparator(output, frame);
                            output.Append('"').Append(s, i + 1, scan.SafeEnd - i - 1).Append('"');
                            if (Commit(output, stack, ref state) is { } completedRoot)
                            {
                                return completedRoot;
                            }

                            return ResolveUnwind(output, stack, allow, tailCommitted: true, Incomplete(allow));
                        }

                        if (!scan.Valid)
                        {
                            return ResolveMalformed(output, stack, allow, "Invalid string content.");
                        }

                        AppendValueSeparator(output, frame);
                        output.Append(s, i, scan.End - i + 1);
                        i = scan.End + 1;
                        if (Commit(output, stack, ref state) is { } done)
                        {
                            return done;
                        }

                        break;
                    }

                    if (c == '{' || c == '[')
                    {
                        if (stack.Count + 1 > options.MaxDepth)
                        {
                            throw new LlmJsonException($"Nesting depth exceeds MaxDepth ({options.MaxDepth}).");
                        }

                        AppendValueSeparator(output, frame);
                        output.Append(c);
                        stack.Add(new Frame { IsObject = c == '{', CommittedLength = output.Length });
                        state = c == '{' ? State.ExpectKey : State.ExpectValue;
                        i++;
                        break;
                    }

                    if (c == ']' && frame is { IsObject: false })
                    {
                        i++;
                        if (Close(output, stack, ref state) is { } done)
                        {
                            return done;
                        }

                        break;
                    }

                    if (c is 't' or 'f' or 'n')
                    {
                        string literal = c == 't' ? "true" : c == 'f' ? "false" : "null";
                        if (MatchesAt(s, i, literal))
                        {
                            AppendValueSeparator(output, frame);
                            output.Append(literal);
                            i += literal.Length;
                            if (Commit(output, stack, ref state) is { } done)
                            {
                                return done;
                            }

                            break;
                        }

                        if (IsPrefixToEnd(s, i, literal))
                        {
                            Allow needed = c == 'n' ? Allow.Null : Allow.Booleans;
                            if ((allow & needed) == 0)
                            {
                                return ResolveUnwind(output, stack, allow, tailCommitted: false, Incomplete(allow));
                            }

                            AppendValueSeparator(output, frame);
                            output.Append(literal);
                            if (Commit(output, stack, ref state) is { } completedRoot)
                            {
                                return completedRoot;
                            }

                            return ResolveUnwind(output, stack, allow, tailCommitted: true, Incomplete(allow));
                        }

                        return ResolveMalformed(output, stack, allow, $"Unrecognized literal at position {i}.");
                    }

                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        int start = i;
                        while (i < n && IsNumberChar(s[i]))
                        {
                            i++;
                        }

                        ReadOnlySpan<char> token = s.AsSpan(start, i - start);
                        if (i >= n)
                        {
                            return ResolveNumberTail(output, stack, allow, frame, token, ref state);
                        }

                        if (!IsValidNumber(token))
                        {
                            return ResolveMalformed(output, stack, allow, $"Malformed number at position {start}.");
                        }

                        AppendValueSeparator(output, frame);
                        output.Append(token);
                        if (Commit(output, stack, ref state) is { } done)
                        {
                            return done;
                        }

                        break;
                    }

                    return ResolveMalformed(output, stack, allow, $"Unexpected character '{c}' at position {i}.");

                case State.AfterValue:
                    if (c == ',')
                    {
                        state = frame!.IsObject ? State.ExpectKey : State.ExpectValue;
                        i++;
                        break;
                    }

                    if ((c == ']' && !frame!.IsObject) || (c == '}' && frame!.IsObject))
                    {
                        i++;
                        if (Close(output, stack, ref state) is { } done)
                        {
                            return done;
                        }

                        break;
                    }

                    if (c is ']' or '}')
                    {
                        return ResolveMalformed(output, stack, allow, $"Mismatched closing bracket at position {i}.");
                    }

                    state = frame!.IsObject ? State.ExpectKey : State.ExpectValue;
                    break;

                case State.ExpectKey:
                    if (c == '}')
                    {
                        i++;
                        if (Close(output, stack, ref state) is { } done)
                        {
                            return done;
                        }

                        break;
                    }

                    if (c == '"')
                    {
                        StringScan scan = ScanString(s, i);
                        if (!scan.Closed)
                        {
                            return ResolveUnwind(output, stack, allow, tailCommitted: false, Incomplete(allow));
                        }

                        if (!scan.Valid)
                        {
                            return ResolveMalformed(output, stack, allow, "Invalid property name content.");
                        }

                        if (frame!.HasCommitted)
                        {
                            output.Append(',');
                        }

                        output.Append(s, i, scan.End - i + 1);
                        i = scan.End + 1;
                        state = State.AfterKey;
                        break;
                    }

                    return ResolveMalformed(output, stack, allow, $"Expected a property name at position {i}.");

                case State.AfterKey:
                    if (c == ':')
                    {
                        output.Append(':');
                        i++;
                        state = State.ExpectValue;
                        break;
                    }

                    return ResolveMalformed(output, stack, allow, $"Expected ':' at position {i}.");

                default:
                    throw new LlmJsonException("Unreachable parser state.");
            }
        }
    }

    private static string ResolveNumberTail(StringBuilder output, List<Frame> stack, Allow allow, Frame? frame, ReadOnlySpan<char> token, ref State state)
    {
        bool validAsIs = IsValidNumber(token);
        if (stack.Count == 0)
        {
            if (validAsIs)
            {
                return output.Append(token).ToString();
            }

            if ((allow & Allow.Numbers) != 0 && TryTrimNumberTail(token, out ReadOnlySpan<char> prefix))
            {
                return output.Append(prefix).ToString();
            }

            throw new LlmJsonException("Malformed number at end of input.");
        }

        if ((allow & Allow.Numbers) != 0)
        {
            if (validAsIs)
            {
                AppendValueSeparator(output, frame);
                output.Append(token);
                Commit(output, stack, ref state);
                return ResolveUnwind(output, stack, allow, tailCommitted: true, Incomplete(allow));
            }

            if (TryTrimNumberTail(token, out ReadOnlySpan<char> prefix))
            {
                AppendValueSeparator(output, frame);
                output.Append(prefix);
                Commit(output, stack, ref state);
                return ResolveUnwind(output, stack, allow, tailCommitted: true, Incomplete(allow));
            }
        }

        return ResolveUnwind(output, stack, allow, tailCommitted: false, Incomplete(allow));
    }

    private static string ResolveMalformed(StringBuilder output, List<Frame> stack, Allow allow, string message)
    {
        if (stack.Count == 0)
        {
            throw new LlmJsonException(message);
        }

        return ResolveUnwind(output, stack, allow, tailCommitted: false, message);
    }

    private static string ResolveUnwind(StringBuilder output, List<Frame> stack, Allow allow, bool tailCommitted, string failureMessage)
    {
        while (stack.Count > 0)
        {
            Frame frame = stack[^1];
            stack.RemoveAt(stack.Count - 1);
            if (!tailCommitted)
            {
                output.Length = frame.CommittedLength;
            }

            if ((allow & (frame.IsObject ? Allow.Objects : Allow.Arrays)) != 0)
            {
                output.Append(frame.IsObject ? '}' : ']');
                tailCommitted = true;
            }
            else
            {
                tailCommitted = false;
            }
        }

        if (!tailCommitted)
        {
            throw new LlmJsonException(failureMessage);
        }

        return output.ToString();
    }

    private static string? Commit(StringBuilder output, List<Frame> stack, ref State state)
    {
        if (stack.Count == 0)
        {
            return output.ToString();
        }

        Frame frame = stack[^1];
        frame.CommittedLength = output.Length;
        frame.HasCommitted = true;
        state = State.AfterValue;
        return null;
    }

    private static string? Close(StringBuilder output, List<Frame> stack, ref State state)
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

    private static StringScan ScanString(string s, int start)
    {
        int n = s.Length;
        int i = start + 1;
        int safeEnd = i;
        bool poisoned = false;
        bool pendingHighEscape = false;
        bool pendingHighLiteral = false;

        while (i < n)
        {
            char c = s[i];
            if (c == '"')
            {
                return new StringScan
                {
                    Closed = true,
                    Valid = !poisoned && !pendingHighEscape && !pendingHighLiteral,
                    End = i,
                    SafeEnd = safeEnd,
                };
            }

            if (c == '\\')
            {
                if (i + 1 >= n)
                {
                    break;
                }

                char escape = s[i + 1];
                if (escape == 'u')
                {
                    if (i + 6 > n)
                    {
                        break;
                    }

                    if (!TryParseHex4(s, i + 2, out int codeUnit))
                    {
                        poisoned = true;
                        i += 2;
                        continue;
                    }

                    if (pendingHighLiteral)
                    {
                        poisoned = true;
                    }

                    if (codeUnit is >= 0xD800 and <= 0xDBFF)
                    {
                        if (pendingHighEscape)
                        {
                            poisoned = true;
                        }

                        pendingHighEscape = true;
                    }
                    else if (codeUnit is >= 0xDC00 and <= 0xDFFF)
                    {
                        if (pendingHighEscape)
                        {
                            pendingHighEscape = false;
                            i += 6;
                            if (!poisoned)
                            {
                                safeEnd = i;
                            }

                            continue;
                        }

                        poisoned = true;
                    }
                    else if (pendingHighEscape)
                    {
                        poisoned = true;
                        pendingHighEscape = false;
                    }

                    i += 6;
                    if (!poisoned && !pendingHighEscape)
                    {
                        safeEnd = i;
                    }

                    continue;
                }

                if ("\"\\/bfnrt".IndexOf(escape) >= 0)
                {
                    if (pendingHighEscape || pendingHighLiteral)
                    {
                        poisoned = true;
                        pendingHighEscape = false;
                        pendingHighLiteral = false;
                    }

                    i += 2;
                    if (!poisoned)
                    {
                        safeEnd = i;
                    }

                    continue;
                }

                poisoned = true;
                i += 2;
                continue;
            }

            if (char.IsHighSurrogate(c))
            {
                if (pendingHighEscape || pendingHighLiteral)
                {
                    poisoned = true;
                }

                pendingHighLiteral = true;
                i++;
                continue;
            }

            if (char.IsLowSurrogate(c))
            {
                if (pendingHighLiteral)
                {
                    pendingHighLiteral = false;
                    i++;
                    if (!poisoned)
                    {
                        safeEnd = i;
                    }

                    continue;
                }

                poisoned = true;
                i++;
                continue;
            }

            if (pendingHighEscape || pendingHighLiteral)
            {
                poisoned = true;
                pendingHighEscape = false;
                pendingHighLiteral = false;
            }

            if (c < 0x20)
            {
                poisoned = true;
                i++;
                continue;
            }

            i++;
            if (!poisoned)
            {
                safeEnd = i;
            }
        }

        return new StringScan { Closed = false, Valid = !poisoned, End = n, SafeEnd = safeEnd };
    }

    private static bool TryParseHex4(string s, int start, out int value)
    {
        value = 0;
        for (int i = start; i < start + 4; i++)
        {
            int digit = HexDigit(s[i]);
            if (digit < 0)
            {
                return false;
            }

            value = (value << 4) | digit;
        }

        return true;
    }

    private static int HexDigit(char c)
    {
        return c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };
    }

    private static bool MatchesAt(string s, int start, string literal)
    {
        return start + literal.Length <= s.Length && s.AsSpan(start, literal.Length).SequenceEqual(literal);
    }

    private static bool IsPrefixToEnd(string s, int start, string literal)
    {
        ReadOnlySpan<char> remainder = s.AsSpan(start);
        return remainder.Length < literal.Length && literal.AsSpan(0, remainder.Length).SequenceEqual(remainder);
    }

    private static bool IsJsonWhitespace(char c)
    {
        return c is ' ' or '\t' or '\r' or '\n';
    }

    private static bool IsNumberChar(char c)
    {
        return c is (>= '0' and <= '9') or '-' or '+' or '.' or 'e' or 'E';
    }

    internal static bool IsValidNumber(ReadOnlySpan<char> token)
    {
        int i = 0;
        if (i < token.Length && token[i] == '-')
        {
            i++;
        }

        if (i >= token.Length)
        {
            return false;
        }

        if (token[i] == '0')
        {
            i++;
        }
        else if (token[i] is >= '1' and <= '9')
        {
            while (i < token.Length && token[i] is >= '0' and <= '9')
            {
                i++;
            }
        }
        else
        {
            return false;
        }

        if (i < token.Length && token[i] == '.')
        {
            i++;
            int fractionStart = i;
            while (i < token.Length && token[i] is >= '0' and <= '9')
            {
                i++;
            }

            if (i == fractionStart)
            {
                return false;
            }
        }

        if (i < token.Length && token[i] is 'e' or 'E')
        {
            i++;
            if (i < token.Length && token[i] is '+' or '-')
            {
                i++;
            }

            int exponentStart = i;
            while (i < token.Length && token[i] is >= '0' and <= '9')
            {
                i++;
            }

            if (i == exponentStart)
            {
                return false;
            }
        }

        return i == token.Length;
    }

    internal static bool TryTrimNumberTail(ReadOnlySpan<char> token, out ReadOnlySpan<char> prefix)
    {
        int exponent = token.LastIndexOfAny('e', 'E');
        ReadOnlySpan<char> candidate = exponent > 0 ? token[..exponent] : token;
        while (candidate.Length > 0 && candidate[^1] is '.' or '+' or '-' or 'e' or 'E')
        {
            candidate = candidate[..^1];
        }

        prefix = candidate;
        return candidate.Length > 0 && IsValidNumber(candidate);
    }
}
