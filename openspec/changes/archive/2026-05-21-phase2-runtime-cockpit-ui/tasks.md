## 1. Workflow Model

- [x] 1.1 Create `src/DoipSimulator.WebConsole/src/connectionWorkflow.ts` with step ids, step state derivation, default selection, evidence helpers, DID preview formatting, and copy text helpers.
- [x] 1.2 Run `npm.cmd run build` in `src/DoipSimulator.WebConsole` to verify TypeScript integration.

## 2. Cockpit Components

- [x] 2.1 Create `ConnectionStepList.vue` for the four-step selectable workflow list.
- [x] 2.2 Create `ConnectionStepDetail.vue` for parameter-first, troubleshooting-first, copy action, and evidence detail rendering.
- [x] 2.3 Create `EvidenceSummaryGrid.vue` for latest traffic, PCAP/event evidence, and DID preview.
- [x] 2.4 Create `RuntimeCockpitPanel.vue` to compose the cockpit, shutdown action, selected step state, and copy feedback.
- [x] 2.5 Add cockpit CSS to `src/DoipSimulator.WebConsole/src/styles.css` using the existing dark control-desk visual system.
- [x] 2.6 Run `npm.cmd run build` in `src/DoipSimulator.WebConsole`.

## 3. Dashboard Integration

- [x] 3.1 Update `DashboardView.vue` imports and state to load DID samples, PCAP status, recent phase events, and the cockpit snapshot.
- [x] 3.2 Update runtime refresh/event handling so cockpit evidence stays current and optional evidence failures do not blank the Overview.
- [x] 3.3 Replace the old Overview connection-guide section with `RuntimeCockpitPanel` while preserving telemetry, realtime rail, metrics/config sections as appropriate, and shutdown confirmation behavior.
- [x] 3.4 Run `npm.cmd run build` in `src/DoipSimulator.WebConsole`.

## 4. Lightweight Smoke And Docs

- [x] 4.1 Create `scripts/web-console-runtime-cockpit-smoke.ps1` to verify cockpit source files, Overview integration labels, and frontend build.
- [x] 4.2 Document the cockpit smoke command in `README.md`.
- [x] 4.3 Run `powershell -ExecutionPolicy Bypass -File .\scripts\web-console-runtime-cockpit-smoke.ps1`.

## 5. Verification

- [x] 5.1 Run `openspec validate phase2-runtime-cockpit-ui --strict`.
- [x] 5.2 Run `openspec validate --all --strict`.
- [x] 5.3 Run the frontend production build.
- [x] 5.4 Run relevant backend regression tests if no backend code changes are made; run the full .NET solution test suite if backend or shared contracts are touched.
- [x] 5.5 Perform manual browser review to confirm the Overview shows the runtime cockpit, four steps, selectable detail panel, copy feedback, shutdown action, right realtime rail, and no obvious desktop overflow.
