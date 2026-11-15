> **Maintained by [Joseph Ukeje](https://github.com/UkejeJoseph)**

# LlmJson

The .NET library for JSON that comes out of an LLM: extract it from prose, repair it, and parse it while it streams. Zero external dependencies.

Every developer wiring an app to an LLM hits two problems in week one:

1. **The model streams JSON token by token.** You cannot parse it until it is complete, so your UI freezes on a spinner instead of filling in live.
2. **Models return almost-JSON.** Markdown fences around the object, single quotes, trailing commas, Python `True` and `None`, a friendly sentence before and after the payload.

The JavaScript ecosystem solved both years ago: `partial-json` sees 18.6M downloads a month and `jsonrepair` 11.7M. On NuGet there has been no streaming partial parser (Microsoft has publicly punted on streaming structured output, see dotnet/extensions#6007) and only weak repair options. LlmJson is both, in one dependency-free package that works with any SDK, because the streaming API takes a plain `IAsyncEnumerable<string>`.

## Install

```
dotnet add package LlmJson
```

## Quickstart

One call takes raw model output (prose, fence, dirty JSON, even cut off mid-generation) to a typed value:

````csharp
using System.Text.Json;
using LlmJson;

string raw = """
    Here's the result you asked for:

    ```json
    {'sentiment': 'positive', 'confidence': 0.87, 'reasons': ['tone', 'word choice',]}
    ```

    Let me know if you need more detail!
    """;

Verdict? verdict = Llm.CoerceTo<Verdict>(raw, new(JsonSerializerDefaults.Web));
Console.WriteLine($"{verdict!.Sentiment} at {verdict.Confidence:P0}: {string.Join(", ", verdict.Reasons!)}");
// positive at 87%: tone, word choice

record Verdict(string? Sentiment, double Confidence, string[]? Reasons);
````

## Streaming: live typed snapshots

`JsonStream.Snapshots<T>` turns the token stream of a JSON generation into a stream of progressively filled values of `T`. This sample simulates a token stream and compiles as pasted:

```csharp
using System.Text.Json;
using LlmJson;

string generated = """{"name": "Ada Lovelace", "skills": ["mathematics", "computing"], "age": 36}""";

await foreach (Person? snapshot in JsonStream.Snapshots<Person>(TokenStream(), new(JsonSerializerDefaults.Web)))
{
    Console.WriteLine($"name={snapshot?.Name,-13} skills=[{string.Join(", ", snapshot?.Skills ?? [])}] age={snapshot?.Age}");
}

async IAsyncEnumerable<string> TokenStream()
{
    for (int i = 0; i < generated.Length; i += 8)
    {
        yield return generated.Substring(i, Math.Min(8, generated.Length - i));
        await Task.Delay(10);
    }
}

record Person(string? Name, string[]? Skills, int Age);
```

Output:

```
name=              skills=[] age=0
name=Ada Lo        skills=[] age=0
name=Ada Lovelace  skills=[] age=0
name=Ada Lovelace  skills=[mat] age=0
name=Ada Lovelace  skills=[mathematics] age=0
name=Ada Lovelace  skills=[mathematics, comp] age=0
name=Ada Lovelace  skills=[mathematics, computing] age=0
name=Ada Lovelace  skills=[mathematics, computing] age=36
```

Because the input is just `IAsyncEnumerable<string>`, any provider plugs in by yielding its text deltas. Illustrative wiring (SDK types elided):

```text
OpenAI:    await foreach (var update in chatClient.CompleteChatStreamingAsync(...))
               foreach (var part in update.ContentUpdate) yield return part.Text;

Anthropic: await foreach (var evt in client.Messages.CreateStreamingAsync(...))
               if (evt.Delta?.PartialJson is { } d) yield return d;   // tool input deltas

M.E.AI:    await foreach (var update in chatClient.GetStreamingResponseAsync(...))
               yield return update.Text;
```

Notes:

- Chunks that do not change the snapshot (whitespace, a comma, half an escape sequence) are deduplicated: consumers only see distinct states.
- Typed snapshots that transiently fail to bind (an enum value cut mid-word) are skipped rather than thrown.
- The untyped `JsonStream.Snapshots(...)` overload yields `JsonElement` snapshots for schema-free consumption.

## Partial parsing

`PartialJson.Parse` completes any truncated JSON prefix into a valid snapshot: open strings, arrays, and objects are closed; a dangling key or an incomplete literal tail is dropped.

```csharp
using LlmJson;

var snapshot = PartialJson.Parse("""{"scores": [88, 92, 7""");
Console.WriteLine(snapshot.GetRawText()); // {"scores":[88,92,7]}
```

Semantics mirror the npm [partial-json](https://github.com/promplate/partial-json-parser-js) parser, including its `Allow` flags: a disallowed partial type means the incomplete value is omitted from the snapshot rather than completed. The npm test suite is ported into this repository's tests.

| Input | Options | Snapshot |
|---|---|---|
| `["` | `Allow.Arrays` | `[]` |
| `["` | `Allow.Arrays \| Allow.Strings` | `[""]` |
| `{"": "` | `Allow.Objects` | `{}` |
| `{"a": tru` | `Allow.Objects \| Allow.Booleans` | `{"a":true}` |
| `-1.25e+` | `Allow.Numbers` | `-1.25` |
| `"` | anything without `Allow.Strings` | throws `LlmJsonException` |

API:

| Method | Purpose |
|---|---|
| `PartialJson.Parse(string, PartialOptions?)` | Truncated prefix to `JsonElement` snapshot |
| `PartialJson.TryParse(string?, out JsonElement, PartialOptions?)` | Non-throwing variant |
| `PartialJson.Deserialize<T>(string, JsonSerializerOptions?, PartialOptions?)` | Snapshot bound to `T`; the missing tail keeps default values |
| `PartialJson.TryDeserialize<T>(string, out T?, JsonSerializerOptions?, PartialOptions?)` | Non-throwing typed variant; false when no snapshot binds |

`PartialOptions` carries the `Allow` flags (default `Allow.All`) and `MaxDepth` (default 64, the `System.Text.Json` default; configurable up to 1,000,000, and deeper input is cleanly rejected with `LlmJsonException`).

Two notes on typed binding (`Deserialize<T>` and `CoerceTo<T>`): the "missing tail keeps default values" promise applies to non-`required` members; a C# `required` property missing from a snapshot throws, exactly as `System.Text.Json` always does. And snapshots bind case-sensitively unless you pass options such as `new JsonSerializerOptions(JsonSerializerDefaults.Web)`, as the samples here do.

## Repair

`JsonRepair.Repair` fixes what LLMs actually emit, and repair is idempotent. The pass-through guarantee is precise: input that is already valid JSON as given is returned unchanged, the same string instance, via a validity fast path. Anything that actually needs repair is re-encoded compactly, except that fenced content which is valid once unfenced is returned as written inside the fence.

```csharp
using LlmJson;

string repaired = JsonRepair.Repair("{'active': True, 'note': None, 'tags': ['a' 'b',],}");
// {"active":true,"note":null,"tags":["a","b"]}
```

| Fix | Example |
|---|---|
| Markdown code fences | ` ```json ` and bare ` ``` ` fences at line start (the CommonMark rule), LF or CRLF; backticks inside string values are left alone |
| Single-quoted strings and keys | `{'a': 'b'}` |
| Smart quotes | `{“a”: “b”}`, `{‘a’: ‘b’}` |
| Unquoted keys | `{retries: 3}` |
| Trailing commas | `[1, 2, 3,]` |
| Missing commas where unambiguous | `{"a":1 "b":2}`, `[1 2]` |
| Python literals | exactly `True`/`TRUE` and `False`/`FALSE` to `true`/`false`; `None`/`NONE`/`Null`/`NULL` to `null` (not `nil`, `Yes`, or `No`) |
| JavaScript leftovers | `undefined`, `NaN`, `Infinity`, `-Infinity` become `null` |
| Concatenated values and JSONL | `{"a":1}{"b":2}` becomes `[{"a":1},{"b":2}]`, one-value-per-line likewise |
| Ellipsis placeholders | `[1, 2, 3, ...]` becomes `[1,2,3]` (`...` and `…` elements are dropped) |
| Comments | `// line` and `/* block */` |
| Unescaped control characters in strings | raw newlines and tabs become `\n`, `\t` |
| Unquoted string values | `{"status": ok}` becomes `{"status":"ok"}` |
| Number formats | `+1`, `.5`, `5.` become `1`, `0.5`, `5`; a dangling exponent (`1.2e`) is trimmed to `1.2` |
| Mismatched or missing closers | `[1, 2}` becomes `[1,2]`; truncation is delegated to the partial engine |

One thing repair deliberately refuses to guess: Python tuple or set syntax (`{'coords': (1, 2)}`) throws `LlmJsonException` instead of mangling the value.

The repair semantics are inspired by the [jsonrepair](https://github.com/josdejong/jsonrepair) library's canonical cases; the implementation is an independent clean-room state machine, not a port of its code.

## Extraction

`Llm.ExtractJson` finds the first JSON value in surrounding prose, fenced or not, and returns it verbatim (or the unterminated tail for a generation cut off mid-stream). `Llm.Coerce` / `Llm.TryCoerce` / `Llm.CoerceTo<T>` chain extract, repair, and parse in one call. Candidate selection prefers fenced content over prose braces, complete balanced values over truncated tails, and non-empty results over braces that repair to an empty `{}`.

```csharp
using LlmJson;

string? json = Llm.ExtractJson("The config is {\"retries\": 3} as discussed.");
// {"retries": 3}
```

## Safe parsing (never throws)

Both typed terminals have a non-throwing twin that returns `false` instead of raising when the input cannot be coerced, completed, or bound, following the `Try*` pattern used throughout the library. Use them on any path where model output is untrusted and a `catch` would be noise:

```csharp
using LlmJson;

if (Llm.TryCoerceTo(rawModelOutput, out Verdict? verdict, new(JsonSerializerDefaults.Web)))
{
    // verdict bound successfully
}

if (PartialJson.TryDeserialize(streamedPrefix, out Verdict? partial, new(JsonSerializerDefaults.Web)))
{
    // partial snapshot bound successfully
}

record Verdict(string? Sentiment, double Confidence, string[]? Reasons);
```

They return `false` (with `result` set to `default`) for uncoercible or unbindable input and never swallow programmer errors: a null input still throws `ArgumentNullException`, exactly as the throwing twins do.

## Errors

Anything invalid beyond repair throws `LlmJsonException`, a sealed subclass of `JsonException`, so one existing `catch (JsonException)` handles it. `Try*` variants never throw on bad content: the untyped `TryParse` / `TryCoerce` / `TryRepair` also return `false` on null input, while the typed `TryCoerceTo<T>` / `TryDeserialize<T>` swallow only parse and bind failures and still throw `ArgumentNullException` on a null input, mirroring their throwing twins. Non-`Try` methods throw `ArgumentNullException` on null.

## Differences from the npm libraries

- `Allow` has no `NaN` / `Infinity` flags: JSON has no such values and `JsonElement` cannot represent them. `PartialJson.Parse("Infinity")` throws; `JsonRepair` maps those literals to `null`.
- Lone UTF-16 surrogates are never emitted. JavaScript's `JSON.parse` tolerates them; `System.Text.Json` (correctly, per RFC 8259) does not, so a string cut mid-surrogate-pair or mid-`\uXXXX` escape is truncated at the last cleanly decodable character.
- A number cut right after its decimal point (`12.`) yields `12`. The npm parser drops the value entirely there; keeping the integer part preserves monotonically growing streaming snapshots.
- Uppercase exponent markers (`1.5E+`) are trimmed like lowercase ones; the npm parser only handles lowercase.
- Missing commas between elements or members are tolerated: `[1 2 3]` parses to `[1,2,3]` and `{"a":1 "b":2}` to `{"a":1,"b":2}`, where npm partial-json yields an empty `[]` / `{}`. This is additional recovery and the result is always valid JSON.

## Limitations

Honesty section. Read it before you ship.

- Repairs are heuristics. They are tuned for text you asked an LLM to produce and should never run on data from arbitrary or adversarial sources. A repair can always guess wrong; the guarantee is only that the output is valid JSON, not that it matches the intent.
- This is not a validator. Malformed content inside an allowed container is dropped, npm-style, not reported. If you need to know the JSON was exactly right, parse it with `System.Text.Json` directly.
- A partial snapshot is a snapshot. `{"total": 1` may become `{"total": 12}` a token later. Do not act on numeric or string values until the stream completes.
- Extraction picks the first plausible candidate. Fenced content, balanced values, and non-empty results are preferred, but prose braces that repair to real content can still win over a later payload; prefer fenced model output.
- Streaming re-parses the whole accumulated buffer on every yielding chunk, so cost is quadratic in stream length: thousands of one-token deltas over a large document add up to seconds, and very long generations to worse. Debounce deltas by time (for example, batch every 50 to 100 ms) for long outputs; incremental state is on the roadmap.
- A stream whose accumulated text becomes invalid mid-flight stops yielding new snapshots without a terminal error. Validate the final state yourself when it matters.

## Performance

The numbers below were measured on the committed adversarial tests (`tests/LlmJson.Tests/Adversarial/AdversarialTests.cs`, Release, .NET 8); the tests themselves assert a generous 10 second CI ceiling, so treat the figures as indicative, not contractual:

- 10 MB truncated single-string value: ~30 ms, linear in input size (1 MB takes ~3 ms)
- Truncated object with 100,000 members: ~47 ms
- 10,000-level nesting with `MaxDepth = 10001`: ~260 ms, no stack overflow (the engine is iterative)

Streaming is the exception: per the quadratic note above, feeding ~8,000 one-character chunks through `JsonStream.Snapshots` takes on the order of seconds, which is why long generations should be debounced.

## Roadmap

- `LlmJson.Extensions.AI`: an adapter package with `IChatClient` streaming helpers
- Schema-guided repair: use the target type to resolve ambiguous repairs
- Incremental streaming state to avoid re-scanning the accumulated buffer per chunk

## License

MIT. See [LICENSE](LICENSE).
