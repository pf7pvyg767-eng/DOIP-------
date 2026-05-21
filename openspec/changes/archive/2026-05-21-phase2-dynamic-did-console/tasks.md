## 1. Provider Update API

- [x] 1.1 Add tests for valid provider update, invalid provider update, and unknown DID.
- [x] 1.2 Implement runtime-store provider update behavior and Web API endpoint.
- [x] 1.3 Verify provider updates immediately affect `DidRuntimeStore`, samples, and `0x22` reads.

## 2. WebConsole DID Panel

- [x] 2.1 Extend WebConsole API types and client function for DID provider updates.
- [x] 2.2 Update DID rows to display provider type and keep static hex writes for static DIDs.
- [x] 2.3 Add provider parameter forms for random, sine, and linear providers.
- [x] 2.4 Surface provider validation errors in the DID row and refresh after successful updates.

## 3. Verification

- [x] 3.1 Run focused Core/WebApi tests.
- [x] 3.2 Run frontend build or type check.
- [x] 3.3 Run the full .NET test suite.
- [x] 3.4 Run `openspec validate phase2-dynamic-did-console --strict`.
