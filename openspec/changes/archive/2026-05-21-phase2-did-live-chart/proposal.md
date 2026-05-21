## Why

Dynamic DID values are now configurable and sampleable, but users still need a visual feedback loop to confirm sine, random, and linear behavior. A live chart makes the simulated ECU behavior observable without leaving Web Console.

## What Changes

- Add a WebConsole DID live chart panel.
- Let users select one or more numeric DID samples.
- Keep the most recent 60 seconds or 300 points per DID, whichever limit is reached first.
- Update chart data from `uds.did.read` sample events when available.
- Poll `GET /api/dids/samples` on a fixed interval so charts continue without diagnostic traffic.
- Draw the chart in the app with lightweight SVG or Canvas; no required charting dependency.

## Capabilities

### New Capabilities

- `web-console-did-live-chart`: realtime DID sample charting in WebConsole.

### Modified Capabilities

- `did-sampling-api`: samples are consumed by WebConsole chart polling and event updates.

## Impact

- Web Console chart component: `src/DoipSimulator.WebConsole/src/components/DidLiveChartPanel.vue`
- Web Console dashboard: `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- Web Console API client: `src/DoipSimulator.WebConsole/src/api.ts`
- Web Console styles: `src/DoipSimulator.WebConsole/src/styles.css`
- Optional package metadata if a dependency is added.
