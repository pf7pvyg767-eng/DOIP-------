## 1. Chart Data Client

- [x] 1.1 Add WebConsole API typing for DID samples if not already present.
- [x] 1.2 Add polling logic for `/api/dids/samples`.
- [x] 1.3 Add runtime event handling for `uds.did.read` numeric sample events.

## 2. Chart UI

- [x] 2.1 Create `DidLiveChartPanel.vue`.
- [x] 2.2 Support selecting one or more numeric DIDs.
- [x] 2.3 Render bounded recent samples with SVG or Canvas.
- [x] 2.4 Add the chart panel to the dashboard.

## 3. Verification

- [x] 3.1 Run frontend build or type check.
- [x] 3.2 Run the full .NET test suite to ensure API behavior remains intact.
- [x] 3.3 Run `openspec validate phase2-did-live-chart --strict`.
