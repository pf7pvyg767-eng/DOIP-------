## Context

Task 04 made dynamic DID runtime reads deterministic and testable. Task 05 turns those reads into an explicit sampling surface for Web Console and enriches diagnostic read events so later UI charting can consume real values instead of inferring them from UDS bytes.

The runtime store is already the single place that resolves static and dynamic DID values. The new sampling behavior should reuse that boundary and avoid duplicating provider logic in Web API or UDS services.

## Goals / Non-Goals

**Goals:**

- Provide a reusable DID sample shape with raw hex, optional numeric value, provider type, and sampled timestamp.
- Let Web API sample one DID or all readable DIDs without a diagnostic tester request.
- Include sample fields in `uds.did.read` events for successful reads.
- Keep static non-numeric DIDs usable by exposing raw hex without numeric conversion.

**Non-Goals:**

- Add DID chart UI or WebConsole rendering.
- Add provider editing endpoints.
- Execute scripts or arbitrary expressions.
- Persist sample history in this task.

## Decisions

### Sample at the Runtime Store Boundary

`DidRuntimeStore` should expose sample-oriented data in addition to byte reads. This keeps provider calculation in Core and lets Web API and UDS share the same sample metadata.

Alternative considered: decode samples separately in Web API and UDS. That would duplicate numeric decoding and make event data inconsistent with direct API samples.

### Numeric Value is Optional

Dynamic numeric providers can expose a decoded numeric value. Static fixed-byte DIDs may be arbitrary bytes such as VIN, so their sample will include raw hex and omit `numericValue` unless a numeric interpretation is explicitly available.

Alternative considered: always parse static bytes as a number. That would create misleading values for identifiers, VIN fragments, and arbitrary byte arrays.

### No Sample History Yet

This task returns current samples only. Realtime charts can poll or subscribe to events first, and persistent history can be designed separately if needed.

Alternative considered: add a ring buffer of sample history. That adds retention, memory, and API semantics beyond the current task.

## Risks / Trade-offs

- Dynamic values may change between API calls and UDS reads -> each response includes its own `sampledAt` timestamp.
- Static raw bytes may not be numeric -> numeric value remains nullable.
- All-DID sampling could be expensive with many dynamic DIDs -> initial implementation samples configured readable DIDs once per request and avoids history.

## Migration Plan

No migration is required. Existing DID configuration and UDS read behavior remain valid; the change only adds API endpoints and event data fields.

## Open Questions

None.
