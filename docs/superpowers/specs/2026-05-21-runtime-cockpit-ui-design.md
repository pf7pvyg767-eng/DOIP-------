# Runtime Cockpit UI Design

## Purpose

The Web Console Overview page should evolve from a collection of runtime panels into a diagnostic connection cockpit. Its first job is to help a tester developer understand whether the simulator is running, how to connect a diagnostic tester, where the current connection flow is stuck, and what evidence exists for the last protocol interaction.

This design keeps the current Web Console visual language: dark engineering console, left workspace navigation, top telemetry strip, compact section panels, right realtime observation rail, and status colors for waiting, active, warning, and error states.

## Target User Experience

When the user opens Web Console, they should immediately see:

- The runtime is healthy or unavailable.
- The current DoIP/UDS phase.
- The exact connection parameters needed by a diagnostic tester.
- The four-step connection flow and the active or failed step.
- Recent DoIP/UDS evidence without switching pages.
- Safe runtime shutdown access.

The page should feel like a field debugging tool rather than a marketing dashboard or a static configuration viewer.

## Layout

The existing app shell remains:

- Left sidebar: workspace navigation.
- Topbar: active workspace title and telemetry strip.
- Main Overview area: redesigned diagnostic connection cockpit.
- Right rail: compact realtime observation feed.

The Overview main section becomes a single primary `Diagnostic connection workflow` panel.

Inside the panel:

1. Header row
   - Eyebrow: `Connection`.
   - Title: `Diagnostic connection workflow`.
   - Runtime phase pill, for example `Waiting`, `TCP Connected`, `Routing Activated`, or `UDS Traffic Active`.
   - `Stop runtime` action with existing confirmation behavior.

2. Flow area
   - Left side: four-step list.
   - Right side: selected step detail panel.

3. Evidence summary
   - Latest traffic.
   - PCAP and event evidence.
   - DID preview or link into the DID workspace.

## Connection Steps

The four primary steps are:

1. `UDP Discovery`
   - Shows UDP port, vehicle announcement state, VIN/EID/GID if available.
   - Provides copy action for host and UDP port.

2. `TCP Connect`
   - Shows DoIP TCP endpoint and active socket state.
   - Shows last connection source if known.

3. `Routing Activation`
   - Shows tester source address, ECU logical address, activation response code, and accepted or rejected state.
   - Provides copy actions for logical addresses and the next UDS example action.

4. `UDS Read DID`
   - Shows a recommended first request such as `22 F1 90`.
   - Shows latest positive or negative response, raw response bytes, and decoded DID preview when available.
   - Links to the DID workspace for dynamic DID configuration and charting.

Each step supports these states:

- `not-started`
- `waiting`
- `active`
- `passed`
- `failed`

The active step is selected automatically from runtime state. The user may also manually select any step to inspect details.

## Detail Panel Behavior

The right-side detail panel is state-driven:

- Normal, waiting, or not-started states prioritize parameters: IP, ports, logical addresses, request examples, and copy buttons.
- Failed states prioritize troubleshooting: failure reason, likely causes, and recommended next action.
- Passed and active states prioritize the next action while still showing recent evidence.
- Evidence is always available near the bottom: latest DoIP frame, latest UDS request/response, event timestamp, and PCAP state.

This avoids forcing the user to decide whether they should look at parameters, logs, or troubleshooting text.

## Copy Actions

The UI should support copy actions for:

- Web API endpoint.
- DoIP TCP endpoint.
- DoIP UDP endpoint.
- ECU logical address.
- Tester source address.
- Example UDS `ReadDataByIdentifier` request.
- A compact diagnostic action summary suitable for notes or AI-assisted debugging.

Copy success should be visible but lightweight, such as changing the button label to `Copied` for a short time.

## Evidence Summary

The bottom evidence area should include three compact panels:

- `Latest traffic`: latest DoIP payload type and latest UDS request/response pair.
- `Evidence`: event stream state and PCAP recording state, including file path or byte count when available.
- `DID preview`: current value for one or more key DIDs, including dynamic numeric samples when available.

This area should not duplicate the full Diagnostics, Events, Capture, or Data pages. It should provide enough context to know where to go next.

## Data Sources

The page should use existing real runtime data wherever possible:

- Runtime summary API for endpoints, process/config information, and current runtime state.
- Connections API for active connections and routing activation state.
- Runtime events and recent events API for latest DoIP/UDS evidence.
- Metrics API for active connection count and UDS throughput.
- DID sample API for DID preview.
- PCAP API or current PCAP panel data source for capture status, when available.

No mock data should be introduced into the production UI.

## Error Handling

When the backend is unavailable, the Overview should retain the existing backend-unavailable state.

When a specific data source fails:

- Keep the cockpit shell visible if the main dashboard state is available.
- Mark the affected section as unavailable.
- Do not hide other healthy runtime evidence.

When shutdown is in progress:

- Disable refresh loops that would create noisy failures.
- Show shutdown accepted, waiting, stopped, or failed state.
- Keep the final visible state understandable after the API disconnects.

## Component Boundaries

Recommended component split:

- `RuntimeCockpitPanel.vue`
  - Owns the cockpit layout and selected step.
- `ConnectionStepList.vue`
  - Renders the four-step list and status indicators.
- `ConnectionStepDetail.vue`
  - Renders state-driven parameter, troubleshooting, and evidence detail.
- `EvidenceSummaryGrid.vue`
  - Renders latest traffic, evidence, and DID preview cards.

Existing `DashboardView.vue` should orchestrate data loading and pass real snapshots down, unless extracting a reusable composable becomes cleaner.

## Visual Direction

The implementation should match the current Web Console:

- Use the existing dark palette and CSS variables.
- Keep panel radius at 8px.
- Use compact typography and dense but readable spacing.
- Preserve current navigation and telemetry strip.
- Avoid decorative hero treatment, oversized cards, or marketing layout.

The cockpit should read as a practical diagnostic bench.

## Verification

Minimum verification should include:

- Frontend build succeeds.
- Existing backend tests for runtime summary, shutdown, DID samples, and event APIs remain passing.
- A lightweight UI smoke confirms the Overview renders the workflow labels, current phase, copy buttons, evidence section, and shutdown action.
- Manual browser review confirms the layout does not overflow at common desktop widths.

## Out of Scope

This design does not include:

- A full browser E2E suite.
- MSI installation validation.
- Redesigning all workspaces.
- Replacing the existing right realtime rail.
- Adding new protocol behavior beyond what is needed to display real runtime state.
