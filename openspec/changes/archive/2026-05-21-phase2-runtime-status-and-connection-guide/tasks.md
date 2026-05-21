## 1. Backend Runtime Summary

- [x] 1.1 Add a runtime summary response contract that includes Web API listen address/port, DoIP UDP/TCP/TLS ports, TLS enabled state, VIN, ECU logical address, tester source address whitelist, config path, startedAt, process ID, and active connection count.
- [x] 1.2 Add `GET /api/runtime/summary` in `WebApiApplication` using existing runtime options, loaded configuration, and connection registry snapshots.
- [x] 1.3 Ensure `GET /api/runtime/summary` is read-only and does not mutate configuration, connection, ECU, DID, DTC, PCAP, fault, TLS, or protocol state.
- [x] 1.4 Add backend tests for default runtime summary fields, non-default port values, TLS disabled visibility, missing config path behavior, and active connection count.

## 2. Frontend API Client

- [x] 2.1 Add TypeScript interfaces for the runtime summary response in `src/DoipSimulator.WebConsole/src/api.ts`.
- [x] 2.2 Add a `loadRuntimeSummary()` API helper that calls `GET /api/runtime/summary` and reports request failures consistently with existing dashboard helpers.
- [x] 2.3 Update dashboard state loading so health, config, runtime summary, and metrics can be displayed without hard-coded connection values.

## 3. Connection Guide UI

- [x] 3.1 Add a first-screen connection guide section to the Overview workspace showing Web API endpoint, DoIP UDP/TCP/TLS ports, TLS state, VIN, ECU logical address, tester source address whitelist, config path, startedAt, process ID, and active connection count.
- [x] 3.2 Add unavailable/fallback rendering for runtime summary failures while keeping health/config sections usable when their API calls succeed.
- [x] 3.3 Update styling in `styles.css` so the connection guide is scannable, compact, and consistent with the existing console layout.
- [x] 3.4 Verify the UI displays backend-provided non-default port and whitelist values rather than hard-coded defaults.

## 4. Runtime Phase Summary

- [x] 4.1 Add frontend state that derives runtime phase from runtime summary, connection snapshots, and runtime events.
- [x] 4.2 Display the initial phase as API ready and waiting for DoIP discovery or tester connection when no active connections exist.
- [x] 4.3 Update the phase to TCP connected when an open TCP connection is observed without Routing Activation.
- [x] 4.4 Update the phase to routing activated when a connection snapshot or event reports Routing Activation.
- [x] 4.5 Update the phase to UDS traffic active when UDS request or response events are received.
- [x] 4.6 Refresh snapshots after event stream reconnect so the phase summary recomputes from current backend state.

## 5. Validation

- [x] 5.1 Run backend tests covering the runtime summary endpoint.
- [x] 5.2 Run existing realtime observation and dashboard-related tests, or document the closest available verification when no focused frontend test harness exists.
- [x] 5.3 Run `dotnet test .\DoipSimulator.sln --no-restore`.
- [x] 5.4 Run `cd .\src\DoipSimulator.WebConsole; npm run build`.
- [x] 5.5 Manually verify that opening the Web Console shows the connection guide and that connecting a tester updates the phase summary through TCP connected, routing activated, and UDS traffic active states.
