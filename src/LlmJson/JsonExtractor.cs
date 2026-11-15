namespace LlmJson;

internal readonly record struct JsonCandidate(string Text, bool IsBalanced);

internal static class JsonExtractor
{
    private const string Fence = "```";
    private const int MaxCandidates = 8;

    internal static IEnumerable<JsonCandidate> Candidates(string text)
    {
        if (text.Contains(Fence, StringComparison.Ordinal))
        {
            string fenced = RepairEngine.StripFence(text);
            foreach (JsonCandidate candidate in ScanBalanced(fenced))
            {
                yield return candidate;
            }
        }

        foreach (JsonCandidate candidate in ScanBalanced(text))
        {
            yield return candidate;
        }
    }

    private static IEnumerable<JsonCandidate> ScanBalanced(string text)
    {
        int yielded = 0;
        for (int i = 0; i < text.Length && yielded < MaxCandidates; i++)
        {
            char c = text[i];
            if (c != '{' && c != '[')
            {
                continue;
            }

            int end = FindBalancedEnd(text, i);
            if (end >= 0)
            {
                yield return new JsonCandidate(text[i..(end + 1)], IsBalanced: true);
                yielded++;
                continue;
            }

            yield return new JsonCandidate(text[i..], IsBalanced: false);
            yield break;
        }
    }

    private static int FindBalancedEnd(string text, int start)
    {
        int depth = 0;
        int i = start;
        char lastSignificant = '\0';
        while (i < text.Length)
        {
            char c = text[i];
            if (c == '"')
            {
                i = SkipDoubleQuoted(text, i);
                lastSignificant = '"';
                continue;
            }

            if (c == '\'' && lastSignificant is '{' or '[' or ',' or ':')
            {
                i = SkipSingleQuoted(text, i);
                lastSignificant = '\'';
                continue;
            }

            if (c == '{' || c == '[')
            {
                depth++;
                lastSignificant = c;
            }
            else if (c == '}' || c == ']')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }

                lastSignificant = c;
            }
            else if (!char.IsWhiteSpace(c))
            {
                lastSignificant = c;
            }

            i++;
        }

        return -1;
    }

    private static int SkipDoubleQuoted(string text, int start)
    {
        int i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == '"')
            {
                return i + 1;
            }

            i++;
        }

        return i;
    }

    private static int SkipSingleQuoted(string text, int start)
    {
        int i = start + 1;
        while (i < text.Length)
        {
            if (text[i] == '\\')
            {
                i += 2;
                continue;
            }

            if (text[i] == '\'' && IsCloseCandidate(text, i))
            {
                return i + 1;
            }

            i++;
        }

        return i;
    }

    private static bool IsCloseCandidate(string text, int index)
    {
        int next = index + 1;
        while (next < text.Length && (text[next] == ' ' || text[next] == '\t'))
        {
            next++;
        }

        return next >= text.Length || text[next] is ',' or ':' or ']' or '}' or '\r' or '\n';
    }
}
