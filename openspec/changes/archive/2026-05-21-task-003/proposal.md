# Proposal: Configuration Model, Default Configuration, And Validation

**Change ID:** `task-003`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

`task-001` established the backend project structure and `task-002` added the minimal runtime startup path, but there is still no shared simulator configuration contract for the Host, DoIP, UDS, Web, and future ODX-facing layers. Later tasks need a strongly typed configuration model that can be loaded from JSON, generated when missing, saved back to disk, and validated before runtime behavior depends on it.

## Proposed Solution

Add a constrained configuration foundation:

- Define `SimulatorConfig` and nested strongly typed configuration objects for simulator identity, network settings, UDS placeholders, and TLS settings.
- Support JSON configuration load and save.
- Generate a default configuration when the requested configuration file is missing.
- Validate VIN, EID, GID, logical address values, DoIP/Web-relevant ports, and source address whitelist entries.
- Reserve configuration structures for DID, DTC, Routine, Session, SecurityAccess, Flash, and TLS without implementing protocol runtime behavior.

## Scope

### In Scope

- Define `SimulatorConfig`.
- Support JSON configuration load and save.
- Generate a default JSON configuration when configuration is missing.
- Implement basic validation for VIN, EID, GID, logical address, ports, and source address whitelist.
- Reserve configuration structures for DID, DTC, Routine, Session, SecurityAccess, Flash, and TLS.
- Add focused tests for default generation, valid JSON load, validation failures, and save/reload round trip.
- Add or update a sample default simulator configuration if needed.

### Out of Scope

- YAML configuration support.
- ODX/PDX import.
- Web editing for configuration.
- Configuration version migration.
- DoIP or UDS runtime behavior beyond configuration contracts.
- Implementing DID, DTC, Routine, Session, SecurityAccess, Flash, or TLS protocol behavior.

## Open Questions

- The task does not specify exact validation error types or error codes. The implementation should return clear field-specific errors while preserving existing project testing style.
- The task does not specify whether missing configuration generation should write to the requested path automatically or return a default object for the caller to save. The implementation should choose the smallest behavior that satisfies load/save and default-generation acceptance criteria.
- The task does not specify exact placeholder fields for DID, DTC, Routine, Session, SecurityAccess, and Flash entries. The implementation should reserve minimal typed structures without adding protocol semantics.

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Yes | Add configuration model, validator, JSON store/load/save contracts, and reserved nested structures. |
| Host | Maybe | May reference the config store only where needed to expose or test the configuration contract; no runtime protocol behavior. |
| WebApi | No | No configuration editing APIs or new business endpoints. |
| WebConsole | No | No web editing or UI changes. |
| Tests | Yes | Add focused Core tests for defaults, JSON loading, invalid fields, and round-trip persistence. |
| Protocol logic | No | No DoIP/UDS runtime implementation beyond typed configuration contracts. |

## Architecture Considerations

- Configuration contracts should live in `DoipSimulator.Core` so Host, WebApi, and future protocol modules can share the same typed model.
- JSON serialization should use the repository's .NET baseline and standard library conventions unless an existing local pattern requires otherwise.
- Validation should be deterministic and field-specific so apply/test agents can assert clear errors for invalid VIN, port, and logical address inputs.
- Reserved UDS and TLS structures should remain data-only placeholders. They must not initialize protocol handlers, security flows, flashing behavior, certificate loading, or ODX/PDX import.

## Acceptance Criteria

- [x] Default configuration can be generated and passes validation.
- [x] Valid JSON configuration loads into strongly typed objects.
- [x] Invalid VIN returns a clear validation error.
- [x] Invalid port returns a clear validation error.
- [x] Invalid logical address returns a clear validation error.
- [x] Save and reload preserves data.
- [x] Scope check confirms no YAML support, ODX/PDX import, web editing, configuration migration, or DoIP/UDS runtime behavior was added.

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Placeholder structures grow into protocol implementation | Medium | High | Keep reserved structures as typed data contracts only and add explicit scope checks. |
| Validation rules are too strict or too loose without detailed task guidance | Medium | Medium | Implement conventional field-level checks and document any assumptions in tests. |
| JSON round-trip changes casing or null/default handling unexpectedly | Medium | Medium | Add round-trip tests covering the provided sample contract. |
| Default generation path has ambiguous write behavior | Medium | Low | Choose the smallest behavior and cover it with tests so apply can validate consistently. |
