## Why

Users currently need to infer how to connect a diagnostic tester from scattered configuration fields, logs, or documentation. Phase 2 needs the Web Console to behave like a usable product surface: when it opens, it should clearly show whether the simulator is running and exactly how an external diagnostic tester should connect.

## What Changes

- Add a runtime status summary API that returns the simulator's current connection guide data, including Web API endpoint, DoIP ports, TLS status, VIN, ECU logical address, tester source address whitelist, config path, start time, process ID, and active connection count.
- Update the Web Console overview to show a first-screen "connect to this simulator" panel sourced from real runtime/configuration data.
- Add a concise runtime phase indicator that progresses through API ready, waiting for DoIP discovery, TCP connected, routing activated, and UDS traffic active based on real snapshots/events.
- Keep the existing Diagnostics/realtime observation views as detailed drill-down surfaces, while the Overview becomes the shortest path for connecting a tester.
- No breaking protocol changes are introduced.

## Capabilities

### New Capabilities
- `runtime-status-summary`: Provides read-only runtime connection guide data for the Web Console and users connecting diagnostic testers.

### Modified Capabilities
- `web-console-dashboard`: Expands the dashboard from basic health/config summary into a connection guide and current runtime status surface.
- `realtime-observability-ui`: Adds a high-level connection phase summary derived from existing connection snapshots and runtime events.

## Impact

- Backend API: `src/DoipSimulator.WebApi/WebApiApplication.cs` will expose a read-only runtime summary endpoint.
- Frontend API client: `src/DoipSimulator.WebConsole/src/api.ts` will add runtime summary types and loading functions.
- Frontend UI: `DashboardView.vue`, `StatusPanel.vue`, and `styles.css` will render the connection guide and runtime phase indicators.
- Tests: focused backend API tests and frontend build/render checks should verify the new contract and UI states.
