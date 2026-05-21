## Context

The previous dynamic DID provider change added configuration and runtime support for built-in providers. Task 04 focuses on making that behavior operationally trustworthy: reads must evaluate current provider values at runtime, and a real TCP DoIP client must be able to observe those generated values through `0x22`.

`ReadDataByIdentifierService` already depends on `DidRuntimeStore`. The clean boundary is to keep all provider calculation and encoding inside Core, while the UDS service treats configured static and dynamic DIDs as readable byte arrays.

## Goals / Non-Goals

**Goals:**

- Prove static DID reads still return fixed runtime bytes.
- Prove sine and linear providers change according to elapsed runtime time.
- Prove random providers stay inside range and encode using the configured numeric type.
- Keep UDS `0x22` provider-agnostic.
- Prove the TCP DoIP diagnostic path returns a dynamic DID value after Routing Activation.

**Non-Goals:**

- Add new provider types, script execution, or arbitrary expression evaluation.
- Add Web UI DID charts or provider editing controls.
- Change the JSON provider schema that was introduced by the prior provider-model task.
- Require installation/packaging work in this task.

## Decisions

### Keep Provider Evaluation in `DidRuntimeStore`

`DidRuntimeStore.TryRead()` remains the runtime boundary for DID value resolution. Static DIDs return their current stored value, and dynamic DIDs calculate and encode a fresh provider sample.

Alternative considered: move provider evaluation into `ReadDataByIdentifierService`. This would couple UDS protocol behavior to configuration details and make other consumers of runtime DID values inconsistent.

### Use Controllable Time in Tests

Runtime calculations that depend on elapsed time will be verified with a controllable `TimeProvider` or equivalent test clock. Tests can advance time deterministically and assert that sine and linear providers produce different/current values without relying on sleeps.

Alternative considered: use real clock delays in tests. That would make tests slower and more fragile on busy CI machines.

### Validate Through Public Diagnostic Paths

Unit tests cover provider math at the store boundary, while TCP DoIP tests cover the full path: Routing Activation, diagnostic message forwarding, UDS dispatch, runtime store read, and diagnostic response encoding.

Alternative considered: only unit-test the store. That would miss integration regressions where the transport or dispatcher bypasses the runtime store.

## Risks / Trade-offs

- Time-dependent values can be flaky if tests use wall-clock sleeps -> use controllable time for deterministic store-level assertions.
- Random values are intentionally non-deterministic unless seeded -> assert range and encoding, and use seed only where sequence repeatability matters.
- End-to-end TCP tests may be slower than unit tests -> keep the dynamic DID transport scenario narrow and focused.
- Dynamic values may change between expected-value calculation and response inspection -> assert legal encoding/range at the TCP layer unless the test controls the runtime clock.

## Migration Plan

No data migration is required. Existing static DID configuration and prior dynamic DID provider configuration remain valid.

## Open Questions

None.
