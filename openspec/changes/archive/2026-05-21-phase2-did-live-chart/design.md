## Context

Task 05 introduced current DID samples and enriched DID read events. Task 07 should consume those existing surfaces from WebConsole and keep chart behavior entirely client-side.

## Goals / Non-Goals

**Goals:**

- Show current and recent numeric DID values.
- Support multiple selected numeric DIDs.
- Combine event-driven updates with polling fallback.
- Keep chart state bounded to 60 seconds or 300 samples per DID.

**Non-Goals:**

- Store sample history on the server.
- Add new backend endpoints.
- Add a heavy chart dependency unless strictly necessary.

## Decisions

### Use Lightweight SVG

The chart will render with SVG polylines. This avoids adding dependencies and keeps the build simple.

Alternative considered: install a charting library. Current requirements need only simple recent lines, so the dependency is unnecessary.

### Polling Plus Events

The panel polls `/api/dids/samples` periodically and also listens to the existing runtime event WebSocket. Polling keeps charts alive without diagnostic tools; events make tester-triggered reads appear immediately.

Alternative considered: event-only charting. That fails when no diagnostic request is active.

### Client-Side Retention

Each DID series keeps samples newer than 60 seconds and caps at 300 points. This avoids backend state and keeps memory bounded.

## Risks / Trade-offs

- Polling can duplicate points close to WebSocket events -> samples are timestamped and bounded; exact deduplication is not required for this phase.
- SVG can get crowded with many selected DIDs -> limit rendering to selected numeric DIDs and use compact legends.
- Non-numeric DIDs cannot be charted -> only samples with `numericValue` are selectable.

## Migration Plan

No migration is required.

## Open Questions

None.
