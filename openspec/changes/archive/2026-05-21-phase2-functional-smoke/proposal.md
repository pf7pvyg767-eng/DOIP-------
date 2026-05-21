## Why

Phase 2 needs a lightweight functional smoke that checks the core runtime workflow without turning every task into MSI packaging, full UI E2E, or report generation work. The smoke should quickly prove that connection guidance, DoIP/UDS, dynamic DID sampling, and shutdown are usable together.

## What Changes

- Add `scripts/phase2-functional-smoke.ps1`.
- Cover API health, runtime summary, UDP discovery, TCP routing activation, static DID read, dynamic DID read, sample API numeric value, and shutdown API availability.
- Print a clear pass/fail line for each check and exit non-zero on failure.
- Document how to run the smoke and what it intentionally excludes.

## Capabilities

### New Capabilities

- `phase2-functional-smoke`: lightweight local functional smoke for Phase 2 core workflows.

### Modified Capabilities

None.

## Impact

- Smoke script: `scripts/phase2-functional-smoke.ps1`
- Existing local smoke reference: `runs/local-dev/doip-uds-smoke-temp.ps1`
- Docs: `README.md`, `docs/Phase2-Task-Plan.md`
