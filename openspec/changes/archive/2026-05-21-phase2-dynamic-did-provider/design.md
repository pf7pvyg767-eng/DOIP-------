## Context

The current DID stack is centered on fixed hex values in `DidConfig`. `DidRuntimeStore` parses those values once at startup and returns the stored bytes to `0x22`, the DID API, and WebConsole summaries. Phase2 needs dynamic DID data sources such as random values, sine waves, and linear ramps while preserving all existing static DID behavior.

This task is the configuration/runtime model layer for dynamic DIDs. The later charting/UI task can consume these values, but this change should not build the chart surface yet.

## Goals / Non-Goals

**Goals:**

- Add a typed `valueProvider` object to DID configuration.
- Preserve backward compatibility: a DID with no `valueProvider` remains a static hex DID.
- Support `random`, `sine`, and `linear` numeric providers with deterministic validation and byte encoding.
- Ensure `0x22` reads and `GET /api/dids` use the current generated value.
- Keep writable static DID behavior working.
- Add sample dynamic DID entries to the default sample configuration.

**Non-Goals:**

- No script execution or arbitrary expression evaluator.
- No WebConsole chart or provider editor in this task.
- No ODX/PDX dynamic provider import.
- No dynamic DID composition from multiple source DIDs.
- No write/persist support for dynamic provider outputs.

## Decisions

### Dynamic providers are explicit data contracts

`DidConfig` will gain an optional `ValueProvider` object. Supported provider `type` values are `static`, `random`, `sine`, and `linear`. Existing DIDs without `ValueProvider` are treated as `static`.

Rationale: explicit typed fields are safer and easier for AI-assisted development than script strings or ad hoc formulas.

Alternative considered: overload the existing `Value` string with expressions such as `sine(...)`. That would blur validation, make error messages weaker, and introduce parsing work unrelated to the product goal.

### Numeric provider output is encoded by `numericType`

Dynamic providers will produce one numeric sample per read. The sample is encoded to hex bytes using `numericType`. The initial supported set is `uint8`, `uint16`, `int16`, `uint32`, and `int32`; multi-byte values use big-endian byte order.

Rationale: this gives predictable response lengths and matches diagnostic byte-oriented behavior without adding scaling/units complexity.

Alternative considered: allow arbitrary byte lengths. That is more flexible but makes validation and graphing semantics vague.

### Provider evaluation happens at read time

`DidRuntimeStore.TryRead` resolves the current value each time a DID is read. Static entries return the stored bytes. Dynamic entries compute bytes from their provider using the current clock and per-entry state such as random generator seed or start timestamp.

Rationale: `0x22` and `GET /api/dids` already consume the runtime store, so read-time evaluation keeps behavior centralized.

Alternative considered: background timers updating DID values. That introduces scheduling and lifecycle complexity before the UI chart task actually needs streaming.

### Static writes remain static-only

Writable DID API and UDS `0x2E` continue to update static DID runtime values. Dynamic provider DIDs are treated as read-only unless a later change explicitly defines provider mutation behavior.

Rationale: writing a generated value raises unclear questions: should it replace the provider, shift offset, or only override the current sample? Keeping dynamic DIDs read-only avoids accidental semantic drift.

## Risks / Trade-offs

- Dynamic values may change between a `GET /api/dids` display and a subsequent `0x22` read → This is expected for generated signals; tests should assert ranges/patterns rather than exact equality except for seeded random sequences.
- Sine and linear values can exceed the configured numeric type range → Validation should reject impossible static ranges where possible and runtime encoding should clamp generated samples to the numeric type range.
- Seeded random values may become over-specified by tests → Tests should verify repeatability for a fixed seed without tying every implementation detail to a particular RNG algorithm unless the algorithm is documented.
- Existing write APIs may appear available for dynamic DIDs if `writable` is set accidentally → Validation should reject writable dynamic DIDs or runtime write should reject them clearly.
