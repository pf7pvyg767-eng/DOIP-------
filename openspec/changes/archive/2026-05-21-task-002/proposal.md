# Proposal: Runtime Startup, Port Check, And Health Check

**Change ID:** `task-002`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

`task-001` established the repository skeleton and placeholder Host entrypoint, but the Host still does not start the WebApi runtime or expose a minimal health endpoint. The next task needs a constrained runtime path so later work can verify that the control API process can bind to a configured local address and port, report where it is listening, fail clearly when the port is occupied, and stop cleanly on Ctrl+C.

## Proposed Solution

Add the minimum runtime behavior required for the Web control API:

- Extend the Host `run` path so it starts the WebApi process in-process or through the established .NET hosting pattern.
- Add minimal runtime options for Web console/API listen address and port.
- Check listen port availability before startup and fail with a clear message when the port is occupied.
- Print the reachable local URL in the form `http://127.0.0.1:{port}` when startup succeeds.
- Support Ctrl+C graceful shutdown and release the bound port.
- Add `GET /api/health` returning HTTP 200 with minimal health information.

## Scope

### In Scope

- Host starts WebApi for the runtime command path.
- Configurable Web console/API listen address and port.
- Startup-time port occupancy check.
- Console output includes `http://127.0.0.1:{port}`.
- Ctrl+C graceful shutdown for the running Host/WebApi process.
- `GET /api/health` returns HTTP 200 and minimal health data.
- Focused tests for runtime options, health response, startup behavior, and port-in-use failure.

### Out of Scope

- Loading full ECU configuration.
- Starting DoIP network services.
- Implementing frontend business pages.
- Implementing UDS, DID, DTC, Flash, TLS, PCAP, or database capabilities.
- Adding authentication, authorization, persistence, telemetry, or deployment behavior.
- Implementing non-health business APIs.

## Open Questions

- The exact user-facing option names for listen address and port are not specified by the task. The implementation should choose minimal, documented names in the Host help output and tests, without adding a full configuration subsystem.

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Host | Yes | Start WebApi from the runtime command path, parse minimal listen options, print URL, handle Ctrl+C. |
| WebApi | Yes | Expose `GET /api/health` and bind to the configured address/port. |
| Tests | Yes | Add unit/integration coverage for health, options, startup, and port conflict behavior. |
| Frontend | No | No business page or UI implementation. |
| Database | No | No storage or persistence behavior. |
| Protocol logic | No | No DoIP, UDS, DID, DTC, Flash, TLS, or PCAP implementation. |

## Architecture Considerations

- The Host remains the runtime entrypoint and owns command-line/runtime option handling.
- The WebApi project owns HTTP endpoint registration, including `/api/health`.
- Port checking should be implemented as a small runtime startup concern, not as a broad configuration or networking subsystem.
- Health output should be intentionally minimal and stable for smoke tests.
- The change builds directly on `task-001`; it should not replace the established solution structure.

## Acceptance Criteria

- [x] Starting the Host runtime path starts WebApi.
- [x] The WebApi listen address and port can be configured through minimal Host runtime options.
- [x] Successful startup prints `http://127.0.0.1:{port}` to the console.
- [x] `GET /api/health` returns HTTP 200 with minimal health information.
- [x] Starting a second instance on an occupied port fails clearly and mentions the port.
- [x] Ctrl+C stops the process gracefully and releases the port.
- [x] Scope check confirms no full ECU configuration loading, DoIP service, frontend business page, UDS, DID, DTC, Flash, TLS, PCAP, or database capability was added.

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Runtime option design grows into full configuration loading | Medium | Medium | Keep only address/port runtime options and document that full ECU configuration remains out of scope. |
| Port checks are flaky in tests | Medium | Medium | Use loopback addresses, disposable ports, and deterministic occupied-port test setup. |
| Ctrl+C handling is difficult to automate | Medium | Medium | Cover shutdown through host cancellation tokens where possible and leave a manual verification step for signal behavior. |
| WebApi startup introduces frontend or protocol behavior accidentally | Low | High | Add explicit scope checks and tests limited to `/api/health`. |
