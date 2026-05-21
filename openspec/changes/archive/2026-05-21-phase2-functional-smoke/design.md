## Context

The repository already contains a local development smoke under `runs/local-dev`, but Phase 2 needs a named script that focuses on the new minimum acceptance path. It should be runnable against an already-started simulator host and avoid heavyweight installation or full UI automation.

## Goals / Non-Goals

**Goals:**

- Verify the running Web API and DoIP/UDS listener.
- Verify both static and dynamic DID reads.
- Verify sample API returns numeric dynamic DID values.
- Verify shutdown API accepts a controlled shutdown request.
- Produce readable console output.

**Non-Goals:**

- Start or install the simulator.
- Build MSI/installers.
- Run browser E2E or generate formal reports.

## Decisions

### Assume Host Is Already Running

The smoke accepts API/DoIP host and port parameters and checks the running process. This keeps the script small and avoids coupling it to local launch or installation details.

### Configure Dynamic DID Through API

Before reading a dynamic DID, the smoke updates DID `0xF190` to a sine provider through the provider API. This ensures the dynamic path exists even when the starting config is default.

### Shutdown Last

Shutdown is tested last because it intentionally stops the running runtime. Users can opt out with `-SkipShutdown` when they want to keep the host alive.

## Risks / Trade-offs

- The script depends on a running host -> document that prerequisite.
- Shutdown changes runtime state -> make it last and provide `-SkipShutdown`.
- Reusing DID `0xF190` as dynamic changes that DID during the smoke -> this is acceptable for local smoke and can be run against a disposable dev config.

## Migration Plan

No migration is required.

## Open Questions

None.
