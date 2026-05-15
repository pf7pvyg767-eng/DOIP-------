# Implementation Tasks: Runtime Startup, Port Check, And Health Check

**Change ID:** `task-002`

---

## Phase 1: Runtime Options

- [x] 1.1 Add a minimal Host runtime options model for WebApi listen address and port.
- [x] 1.2 Add command-line parsing or equivalent Host entrypoint handling for those options.
- [x] 1.3 Document the selected option names in Host help output and/or README.
- [x] 1.4 Validate invalid address or port inputs with clear command-line feedback.

**Quality Gate:**
- [x] Runtime options are limited to listen address and port.
- [x] No full ECU configuration loading is introduced.

---

## Phase 2: WebApi Startup

- [x] 2.1 Wire the Host runtime `run` path to start WebApi.
- [x] 2.2 Bind WebApi to the configured listen address and port.
- [x] 2.3 Print `http://127.0.0.1:{port}` after successful startup.
- [x] 2.4 Ensure startup does not initialize DoIP, UDS, database, PCAP, TLS, Flash, DID, or DTC behavior.

**Quality Gate:**
- [x] Host runtime starts WebApi successfully on a free port.
- [x] Console output includes the expected local URL.

---

## Phase 3: Port Check And Shutdown

- [x] 3.1 Check whether the requested port is already occupied before starting WebApi.
- [x] 3.2 Fail startup clearly when the requested port is occupied and include the port number in the message.
- [x] 3.3 Support Ctrl+C graceful shutdown through the .NET host cancellation path.
- [x] 3.4 Verify the port is released after shutdown.

**Quality Gate:**
- [x] A second instance using the same port fails clearly.
- [x] Shutdown releases the port.

---

## Phase 4: Health Endpoint And Tests

- [x] 4.1 Add `GET /api/health`.
- [x] 4.2 Return HTTP 200 with minimal health information such as `status`, `version`, and `startedAt`.
- [x] 4.3 Add unit tests for health response shape.
- [x] 4.4 Add integration tests that start WebApi on a disposable port and call `/api/health`.
- [x] 4.5 Add tests or documented manual verification for occupied-port startup failure and Ctrl+C shutdown.

**Quality Gate:**
- [x] `/api/health` returns HTTP 200.
- [x] Backend build and tests pass.
- [x] Scope check confirms no out-of-scope capability was added.

---

## Completion Checklist

- [x] Host starts WebApi from the runtime command path.
- [x] Listen address and port are configurable.
- [x] Startup checks for occupied ports.
- [x] Startup prints `http://127.0.0.1:{port}`.
- [x] Ctrl+C gracefully stops the process.
- [x] `GET /api/health` returns HTTP 200 and minimal health information.
- [x] Occupied-port behavior is tested or manually verified.
- [x] Backend build executed.
- [x] Backend tests executed.
- [x] Scope check confirms no full ECU configuration loading, DoIP network service, frontend business page, UDS, DID, DTC, Flash, TLS, PCAP, or database capability was added.
