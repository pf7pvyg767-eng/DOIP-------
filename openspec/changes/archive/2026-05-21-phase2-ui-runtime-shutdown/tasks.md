## 1. Host And WebApi Shutdown Path

- [x] 1.1 Add a Host-owned runtime shutdown coordinator or callback that can request cancellation of the full simulator runtime.
- [x] 1.2 Pass the shutdown coordinator into `WebApiApplication.Create` from the Host runtime construction path.
- [x] 1.3 Add `POST /api/runtime/shutdown` to WebApi with no required request body.
- [x] 1.4 Publish `system.shutdown.requested` before triggering final shutdown.
- [x] 1.5 Make repeated shutdown requests idempotent while the process is still responding.

## 2. Resource Cleanup

- [x] 2.1 Stop active PCAP recording before triggering final Host cancellation.
- [x] 2.2 Preserve existing Ctrl+C shutdown behavior and stopped event behavior.
- [x] 2.3 Ensure WebApi, UDP DoIP, TCP DoIP, and TLS DoIP listeners are disposed through the existing graceful shutdown path.

## 3. Web Console Shutdown UI

- [x] 3.1 Add a `requestRuntimeShutdown` API client function for `POST /api/runtime/shutdown`.
- [x] 3.2 Add a shutdown action in the overview/service status UI.
- [x] 3.3 Add confirmation UI so cancellation does not call the backend.
- [x] 3.4 After confirmation, show a stopping state and disable repeated submissions.
- [x] 3.5 Treat expected backend disconnection after shutdown as stopped/disconnected state instead of a generic dashboard failure.
- [x] 3.6 Show a clear failure state if the shutdown API fails before shutdown is accepted.

## 4. Tests And Verification

- [x] 4.1 Add backend tests for the shutdown endpoint triggering the shutdown signal and publishing `system.shutdown.requested`.
- [x] 4.2 Add backend tests for PCAP stop-on-shutdown behavior when a recorder is active.
- [x] 4.3 Add process-level smoke coverage that starts Host, calls `POST /api/runtime/shutdown`, and verifies the Host exits.
- [x] 4.4 Verify WebApi and DoIP ports are released after process shutdown.
- [x] 4.5 Add focused frontend test coverage for confirmation, cancel, stopping, disconnected, and failure states where the current frontend test harness supports it.
  - Current WebConsole has no frontend unit test harness; interaction was verified with a Playwright smoke run against Vite + Host.
- [x] 4.6 Run `dotnet test .\DoipSimulator.sln --no-restore`.
- [x] 4.7 Run `npm.cmd run build` in `src/DoipSimulator.WebConsole`.
- [x] 4.8 Run `openspec validate phase2-ui-runtime-shutdown --strict`.
