# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-08-21

### Added

- `Llm.TryCoerceTo<T>` and `PartialJson.TryDeserialize<T>`: non-throwing twins of `Llm.CoerceTo<T>` and `PartialJson.Deserialize<T>`. Each returns `false` with `result` set to `default` when the input cannot be coerced, completed, or bound to `T`, closing the asymmetry where only the two typed terminals still threw. They catch only `JsonException` (which `LlmJsonException` derives from), so a null input still throws `ArgumentNullException` like the throwing twins, and unrelated programmer errors are never swallowed. Purely additive overloads; no existing member changed.

## [0.1.1] - 2026-08-07

### Fixed

- `MaxDepth` is now forwarded to the serializer in `PartialJson.Deserialize<T>` and `Llm.CoerceTo<T>`, so a structure deep enough for `Parse` to accept under a raised `PartialOptions.MaxDepth` also deserializes instead of throwing a `JsonException` at the System.Text.Json default of 64. An explicitly set `serializerOptions.MaxDepth` still wins; System.Text.Json reports an unset limit as 0, which is treated as unset and filled from the partial depth.

### Added

- Culture regression tests: `Deserialize<T>` and `CoerceTo<T>` are exercised under `tr-TR` and `de-DE` for doubles, decimals, a truncated exponent, `DateTime`, case-insensitive name-based enums (no Turkish-I bug), and case-insensitive property binding.
- Documented that `PartialJson.Parse` tolerates missing commas between elements and members (`[1 2 3]`, `{"a":1 "b":2}`), additional recovery beyond the npm libraries.

## [0.1.0] - 2026-08-04

### Added

- `PartialJson` static API: `Parse`, `TryParse`, and `Deserialize<T>` complete any truncated JSON prefix into a valid snapshot, with npm partial-json `Allow` flag semantics (a disallowed partial type is omitted rather than completed) and a configurable `MaxDepth` (default 64).
- `JsonStream.Snapshots` and `JsonStream.Snapshots<T>`: live snapshots from any `IAsyncEnumerable<string>` token stream, with unchanged-snapshot deduplication, transient bind-failure skipping for the typed overload, and cancellation support. Works with any SDK. Documented tradeoff: each yielding chunk re-parses the accumulated buffer, so cost is quadratic in stream length; debounce long generations.
- `JsonRepair.Repair` / `TryRepair`: clean-room heuristic repair of markdown fences (recognized at line start only, so backticks inside string values survive), single and smart quotes, unquoted keys and values, trailing and missing commas, Python `True`/`False`/`None` (with uppercase variants), `undefined`/`NaN`/`Infinity`, comments, unescaped control characters in strings, number formats and dangling exponents, ellipsis placeholder elements, mismatched closers, concatenated top-level values and JSONL (wrapped into an array), and truncation (delegated to the partial engine). Valid JSON passes through unchanged; repair is idempotent. Python tuple syntax is rejected with `LlmJsonException` instead of being mangled.
- `Llm.ExtractJson`: first balanced JSON value in surrounding prose, fenced or not, including the unterminated tail of a generation cut off mid-stream.
- `Llm.Coerce` / `TryCoerce` / `CoerceTo<T>`: one-call extract, repair, and parse, preferring fenced content, balanced values, and non-empty results among candidates.
- `LlmJsonException` (sealed, derived from `JsonException`) for everything invalid beyond repair; `Try*` variants never throw, including on ill-formed UTF-16 input with lone surrogates.
- npm partial-json test suite ported; per-prefix sweep over a canonical document at every truncation point; surrogate-pair and `\uXXXX` chunk-boundary safety; a deterministic lone-surrogate fuzz batch over every Try* API; adversarial size, depth, and timing tests; captured-style real-world LLM output fixtures.
- Zero runtime dependencies; built on the in-box `System.Text.Json`.
- SourceLink (GitHub), deterministic CI builds and `.snupkg` symbol packages.
