## 1. Smoke Script

- [x] 1.1 Create `scripts/phase2-functional-smoke.ps1` with API, UDP, TCP Routing Activation, UDS, sample API, and shutdown checks.
- [x] 1.2 Add clear pass/fail output and non-zero exit behavior.
- [x] 1.3 Add parameters for API base URL, DoIP host/port, logical addresses, and `-SkipShutdown`.

## 2. Documentation

- [x] 2.1 Document how to run the Phase 2 smoke.
- [x] 2.2 Document excluded heavyweight flows: MSI install, full UI E2E, and report generation.

## 3. Verification

- [x] 3.1 Run PowerShell syntax parsing for the smoke script.
- [x] 3.2 Run `openspec validate phase2-functional-smoke --strict`.
