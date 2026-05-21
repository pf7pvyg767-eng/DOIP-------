## Context

The current Web Console can load service health, configuration, metrics, connection snapshots, ECU state, and realtime events. It already has enough raw data for a diagnostic engineer to infer how to connect a tester, but the information is spread across panels and API responses. Phase 2 requires the Overview to become a product-facing connection guide: users should open the console and immediately know whether the simulator is ready and which DoIP parameters to use.

The existing dashboard spec still contains early MVP constraints that limited the dashboard to health/config display and no additional backend contracts. This change deliberately expands that scope while keeping the new behavior read-only and non-invasive.

## Goals / Non-Goals

**Goals:**

- Add a read-only runtime summary contract for connection guide data.
- Show Web API endpoint, DoIP ports, TLS status, VIN, ECU logical address, tester source whitelist, config path, start time, process ID, and active connection count in the Web Console.
- Show a high-level runtime phase that helps users understand whether the simulator is waiting for discovery, connected, routing-activated, or seeing UDS traffic.
- Reuse existing connection snapshots and runtime events where possible.

**Non-Goals:**

- Do not add configuration editing in this change.
- Do not add new DoIP or UDS protocol behavior.
- Do not implement shutdown; that is handled by the separate `phase2-ui-runtime-shutdown` task.
- Do not add charts, DID dynamic behavior, or scenario switching.

## Decisions

1. Add a dedicated runtime summary API instead of overloading `/api/health` or `/api/config`.

   Rationale: `/api/health` should stay minimal, and `/api/config` returns the raw simulator configuration rather than user-facing runtime connection guidance. A dedicated endpoint keeps the UI simple and avoids coupling the frontend to the full configuration shape.

   Alternative considered: derive everything in the frontend from `GET /api/health`, `GET /api/config`, and `GET /api/connections`. This avoids a new endpoint but forces the UI to duplicate runtime-summary logic and still cannot reliably get process ID or resolved Web API endpoint.

2. Keep the runtime summary read-only.

   Rationale: Task 01 is about clarity and connection guidance. Runtime control such as shutdown and configuration mutation belongs to separate changes.

3. Compute the high-level phase in the frontend from snapshots/events, with backend-provided counts as a fallback.

   Rationale: The frontend already receives connection and UDS/DoIP events. Computing the display phase locally keeps the backend API stable and avoids creating a new state machine purely for presentation.

4. Preserve existing Diagnostics details and add a compact Overview guide.

   Rationale: Diagnostics is useful for deep inspection, but the first screen must serve a new user trying to connect a tester. The Overview should summarize the required connection parameters and current phase without duplicating every trace table.

## Risks / Trade-offs

- Runtime endpoint fields may drift from raw config fields → Backend tests should assert the summary reflects the loaded config and runtime options.
- Frontend phase inference could become stale after event stream reconnects → The UI should refresh connection snapshots on mount and after reconnect.
- Existing dashboard spec has outdated scope limitations → This change modifies the scope boundary to allow read-only runtime summary and connection guide behavior.
