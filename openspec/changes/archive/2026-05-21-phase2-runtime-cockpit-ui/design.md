## Context

The Web Console already has a dark control-desk shell with sidebar navigation, a top telemetry strip, an Overview connection guide, controlled runtime shutdown, and a right-side realtime observation rail. Phase 2 now needs the Overview to behave less like a flat status page and more like a diagnostic tester connection workflow.

The approved UI design is documented in `docs/superpowers/specs/2026-05-21-runtime-cockpit-ui-design.md`, with an implementation plan in `docs/superpowers/plans/2026-05-21-runtime-cockpit-ui.md`.

## Goals / Non-Goals

**Goals:**

- Present the Overview as a diagnostic connection cockpit with four steps: UDP Discovery, TCP Connect, Routing Activation, and UDS Read DID.
- Use existing backend runtime APIs and event streams as the source of truth.
- Preserve the current Web Console shell, compact dark visual language, telemetry strip, realtime rail, and shutdown behavior.
- Add copy actions and compact evidence summaries that support real diagnostic tester workflows.
- Keep the implementation small, componentized, and build-verifiable.

**Non-Goals:**

- Do not add new DoIP or UDS protocol behavior.
- Do not introduce mock runtime data into the production UI.
- Do not redesign every Web Console workspace.
- Do not add MSI validation or full browser E2E to the daily development loop.
- Do not add a new frontend dependency unless implementation proves it is unavoidable.

## Decisions

### Decision 1: Keep `DashboardView.vue` as the data orchestrator

`DashboardView.vue` already loads runtime summary, metrics, connection snapshots, recent events, and shutdown state. The cockpit will receive a typed snapshot from `DashboardView.vue` rather than issuing independent API calls inside every child component.

Alternative considered: let each cockpit child component fetch its own data. This would duplicate polling/error handling and make shutdown disconnect states harder to reason about.

### Decision 2: Add a small workflow model helper

A new frontend helper module will derive step states, default selected step, evidence summaries, and copy text from existing API response types. This keeps workflow decisions out of Vue templates and makes the behavior easier to review.

Alternative considered: compute all step state inline in the Vue component. That would work initially but would make the Overview component large and brittle as step rules grow.

### Decision 3: Split cockpit presentation into focused components

The cockpit UI will be split into:

- `RuntimeCockpitPanel.vue`
- `ConnectionStepList.vue`
- `ConnectionStepDetail.vue`
- `EvidenceSummaryGrid.vue`

This matches the approved UI shape and avoids turning `DashboardView.vue` into a large mixed data/layout file.

### Decision 4: Use existing real APIs first

The first implementation will use:

- Runtime summary API.
- Connections API.
- Metrics API.
- Recent events and WebSocket event stream.
- DID sample API.
- PCAP status API.
- Runtime shutdown API.

If a richer backend contract is later needed, it should be proposed as a separate OpenSpec change.

### Decision 5: Add lightweight UI smoke, not full E2E

The change will add a lightweight smoke script that checks source integration and runs the frontend build. Manual browser review remains part of verification for layout and visual fit. This matches the user's earlier preference that installation and heavyweight UI E2E stay out of every daily task.

## Risks / Trade-offs

- [Risk] Runtime events may not contain every exact protocol artifact the cockpit wants to display. → Mitigation: show unavailable states for missing evidence and avoid fake values.
- [Risk] More data sources on Overview can create noisy failures when one endpoint fails. → Mitigation: keep the cockpit shell visible and mark only the affected evidence area unavailable.
- [Risk] Copy actions can fail in browsers that restrict clipboard access. → Mitigation: make copy failures non-fatal and keep the displayed text visible.
- [Risk] The Overview can become too dense. → Mitigation: preserve compact typography and keep full detail pages for Diagnostics, Events, Capture, and Data.
