## Context

The simulator already supports static and built-in dynamic DID providers in configuration. The runtime store owns provider evaluation, and Web API already exposes DID values, samples, and static value writes. Task 06 adds provider editing while preserving the existing split: Core validates/applies runtime changes, Web API exposes a focused endpoint, and Web Console provides a form.

## Goals / Non-Goals

**Goals:**

- Show each DID provider type in the DID panel.
- Keep the existing static hex write workflow for static/writable DIDs.
- Add forms for random, sine, and linear provider parameters.
- Apply valid provider updates without restarting the host.
- Surface validation errors in the UI.

**Non-Goals:**

- Add live charts or sample history.
- Add script/expression providers.
- Add arbitrary partial config editing beyond DID providers.

## Decisions

### Provider Updates Go Through the Runtime Store

`DidRuntimeStore` will expose a provider update operation so the in-memory runtime entry and backing config stay aligned. Web API will not rebuild provider logic itself.

Alternative considered: use `PUT /api/config` for whole-config replacement. That is too clumsy for the Web Console workflow and does not clearly update the existing runtime store.

### Dynamic Provider DIDs Become Read-Only

When a DID is switched to a dynamic provider, the runtime config will mark it non-writable because dynamic bytes are generated. Static provider mode preserves the static value write behavior.

Alternative considered: allow dynamic providers to remain writable. That conflicts with existing validation and runtime write semantics.

### UI Edits Are Form-Based

The Web Console will use explicit fields for each provider type instead of accepting raw JSON. This keeps invalid input localized and makes the AI/user workflow easier to inspect.

## Risks / Trade-offs

- Provider updates can invalidate current static write forms -> reload DID list after every successful provider update.
- Numeric validation must stay consistent with configuration validation -> reuse Core `ConfigValidator`.
- UI can grow dense -> keep provider controls compact and scoped to each DID row.

## Migration Plan

No migration is required. Existing static and dynamic DID configuration remains valid.

## Open Questions

None.
