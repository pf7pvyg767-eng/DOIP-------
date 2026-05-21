## Why

Dynamic DID providers are now available in configuration and runtime sampling, but users still cannot inspect or adjust provider parameters from Web Console. Phase 2 needs this to become a real workflow: configure a DID, read it with `0x22`, and see behavior change without restarting the host.

## What Changes

- Add a Web API endpoint to update a configured DID value provider.
- Let provider updates take effect immediately for runtime store reads and later `0x22` responses.
- Extend the DID list/API model so Web Console can show provider type and parameters.
- Update the WebConsole DID panel to keep static hex writes for static DIDs and show provider forms for random, sine, and linear DIDs.
- Return clear validation errors for invalid provider parameters.
- Do not add live charts in this task.

## Capabilities

### New Capabilities

- `web-console-dynamic-did-config`: WebConsole and WebApi workflow for viewing and editing DID provider configuration.

### Modified Capabilities

- `dynamic-did-provider`: provider changes can be applied at runtime and observed by DID reads.

## Impact

- Web API: `src/DoipSimulator.WebApi/WebApiApplication.cs`
- Core runtime store: `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- Web Console API client: `src/DoipSimulator.WebConsole/src/api.ts`
- Web Console DID panel: `src/DoipSimulator.WebConsole/src/components/DidEditorPanel.vue`
- Web Console styles: `src/DoipSimulator.WebConsole/src/styles.css`
- Tests: Core/WebApi tests and any available frontend build checks.
