## Why

Phase 2 now needs the dynamic DID feature to behave as a real runtime capability, not only as a configuration model. The next gap is to prove that `0x22` reads calculate the current value at read time and that the same behavior is visible through the TCP DoIP diagnostic path.

## What Changes

- Strengthen runtime behavior for dynamic DID providers so sine and linear providers are evaluated from elapsed runtime time on every read.
- Verify random providers always encode values inside their configured range and numeric type.
- Preserve static DID behavior and the existing write/runtime-store behavior for fixed byte values.
- Keep `ReadDataByIdentifierService` provider-agnostic: it reads response bytes from `DidRuntimeStore` and does not inspect provider configuration.
- Add TCP DoIP end-to-end coverage showing a dynamic DID can be read through Routing Activation and UDS `0x22`.
- Do not add provider editing UI, realtime charting, scripting, expressions, or new provider configuration fields in this task.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `dynamic-did-provider`: require runtime evaluation semantics for static, sine, linear, and random DID reads.
- `uds-read-data-by-identifier`: require provider-agnostic UDS reads and TCP DoIP integration for dynamic DID values.

## Impact

- Core runtime store: `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- UDS DID read service: `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- Core tests: `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs`
- UDS tests: `tests/DoipSimulator.Protocols.Uds.Tests/ReadDataByIdentifierServiceTests.cs`
- TCP DoIP integration tests: `tests/DoipSimulator.Transport.Tests/TcpDoipServerTests.cs`
