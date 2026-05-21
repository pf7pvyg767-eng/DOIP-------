## Why

The current Overview page exposes runtime status and connection parameters, but it still reads like a collection of panels rather than a guided diagnostic workflow. Phase 2 needs the first screen to help a tester developer connect, see the current DoIP/UDS phase, inspect the active or failed step, and find evidence without switching through multiple workspaces.

## What Changes

- Replace the Overview connection guide with a runtime cockpit organized around the real diagnostic flow: UDP Discovery, TCP Connect, Routing Activation, and UDS Read DID.
- Add a step list plus selected-step detail panel that uses real runtime summary, connection snapshots, events, DID samples, metrics, and PCAP state.
- Add copy actions for connection parameters and a first UDS ReadDataByIdentifier action.
- Add a compact evidence summary for latest traffic, event/PCAP state, and DID preview.
- Preserve the existing Web Console shell, top telemetry, right realtime rail, shutdown confirmation behavior, and dark engineering visual language.
- Add a lightweight UI smoke entrypoint for the runtime cockpit.

## Capabilities

### New Capabilities

- `web-console-runtime-cockpit`: Defines the structured runtime cockpit workflow, step states, detail panel behavior, copy actions, and evidence summary.

### Modified Capabilities

- `web-console-dashboard`: The Overview dashboard requirement changes from a flat connection guide to a first-screen runtime cockpit that embeds the guided workflow while continuing to use real backend data and controlled shutdown.

## Impact

- Affected frontend files: `DashboardView.vue`, new cockpit Vue components, shared frontend workflow helpers, and `styles.css`.
- Affected documentation/scripts: README smoke instructions and a lightweight runtime cockpit UI smoke script.
- No backend protocol semantics change is intended.
- No new npm runtime dependency is expected.
