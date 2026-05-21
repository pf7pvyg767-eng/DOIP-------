## 1. Core Sample Model

- [x] 1.1 Add focused tests for sampling static raw DID values with no numeric value.
- [x] 1.2 Add focused tests for sampling dynamic numeric DID values with raw hex, numeric value, provider type, and sampled timestamp.
- [x] 1.3 Implement a runtime-store sample contract and methods for one DID and all configured DIDs.

## 2. UDS Event Enrichment

- [x] 2.1 Add tests proving successful static DID reads publish raw sample fields.
- [x] 2.2 Add tests proving successful dynamic DID reads publish numeric sample fields.
- [x] 2.3 Enrich `uds.did.read` event payloads using the runtime-store sample contract.

## 3. Web API

- [x] 3.1 Add tests for `GET /api/dids/{did}/sample` success, unknown DID, and invalid DID route values.
- [x] 3.2 Add tests for `GET /api/dids/samples` returning static and dynamic samples without diagnostic traffic.
- [x] 3.3 Implement the single-sample and all-samples Web API endpoints.

## 4. Verification

- [x] 4.1 Run focused Core, UDS, and WebApi tests.
- [x] 4.2 Run the full .NET test suite.
- [x] 4.3 Run `openspec validate phase2-did-sampling-events-api --strict`.
