## 1. Runtime Store Tests

- [x] 1.1 Add or confirm `DidRuntimeStore` coverage showing static DID reads return the current fixed runtime value.
- [x] 1.2 Add deterministic sine provider coverage using a controlled time source and assert the same DID changes at different sample times.
- [x] 1.3 Add deterministic linear provider coverage using a controlled time source and assert elapsed time changes the encoded value.
- [x] 1.4 Add random provider coverage that repeatedly reads a DID and asserts every encoded value stays inside range and matches the numeric type.

## 2. Runtime Implementation

- [x] 2.1 Adjust `DidRuntimeStore.TryRead()` only as needed so static DIDs return stored bytes and dynamic DIDs calculate current provider bytes at read time.
- [x] 2.2 Ensure provider calculations use an injectable or controllable time source so tests do not depend on wall-clock sleeps.
- [x] 2.3 Preserve existing static DID write/read behavior and existing dynamic provider configuration schema.

## 3. UDS and TCP DoIP Integration

- [x] 3.1 Add or confirm `ReadDataByIdentifierService` coverage proving dynamic DID responses are built from `DidRuntimeStore` bytes and not direct provider parsing.
- [x] 3.2 Add a TCP DoIP integration test that completes Routing Activation, sends UDS `0x22` for a dynamic DID, and receives a positive `0x62` response with a legal generated value.
- [x] 3.3 Confirm DoIP transport remains a forwarding layer and does not calculate or encode DID provider values.

## 4. Verification

- [x] 4.1 Run focused test projects for Core, UDS, and Transport.
- [x] 4.2 Run the full .NET test suite when focused tests pass.
- [x] 4.3 Run `openspec validate phase2-dynamic-did-runtime-calculation --strict`.
