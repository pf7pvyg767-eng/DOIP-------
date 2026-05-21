## Why

Dynamic DID values are now calculated at runtime, but Web Console still needs a clean way to sample those values without waiting for an external diagnostic tester. DID read events also need enough sample metadata to support realtime charts and troubleshooting.

## What Changes

- Add a DID sample contract that includes DID, raw hex value, optional numeric value, provider type, and sampled timestamp.
- Add `GET /api/dids/{did}/sample` to return the current sample for one configured DID.
- Add `GET /api/dids/samples` to return current samples for all configured readable DIDs.
- Enrich successful `uds.did.read` runtime events with the same sample-oriented fields plus connection context.
- For static non-numeric DIDs, return raw hex only and leave numeric value absent.
- Do not add WebConsole charting or DID provider editing UI in this task.

## Capabilities

### New Capabilities

- `did-sampling-api`: exposes current DID samples for Web Console and tests.

### Modified Capabilities

- `uds-read-data-by-identifier`: enriches successful DID read runtime events with current sample data.

## Impact

- Core runtime store: `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- UDS DID read service: `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- Web API: `src/DoipSimulator.WebApi/WebApiApplication.cs`
- Runtime event payloads: `src/DoipSimulator.Core/RuntimeEvents`
- Tests: `tests/DoipSimulator.Core.Tests`, `tests/DoipSimulator.Protocols.Uds.Tests`
