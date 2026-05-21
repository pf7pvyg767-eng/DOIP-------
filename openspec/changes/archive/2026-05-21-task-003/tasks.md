# Implementation Tasks: Configuration Model, Default Configuration, And Validation

**Change ID:** `task-003`

---

## Phase 1: Configuration Contracts

- [x] 1.1 Add `SimulatorConfig` in `DoipSimulator.Core`.
- [x] 1.2 Add strongly typed nested models for simulator identity and network settings.
- [x] 1.3 Add reserved typed structures for DID, DTC, Routine, Session, SecurityAccess, Flash, and TLS configuration.
- [x] 1.4 Ensure the model can represent the task-provided JSON contract without adding protocol runtime behavior.

**Quality Gate:**
- [x] Configuration models compile in `DoipSimulator.Core`.
- [x] Reserved structures are data-only contracts.

---

## Phase 2: Defaults And JSON Store

- [x] 2.1 Add default configuration generation with valid VIN, EID, GID, logical address, ports, and source address whitelist values.
- [x] 2.2 Add JSON load support that deserializes into strongly typed configuration objects.
- [x] 2.3 Add JSON save support for `SimulatorConfig`.
- [x] 2.4 Add missing-configuration handling that returns or creates the default configuration according to the selected minimal store behavior.
- [x] 2.5 Add or update a sample default simulator JSON file if needed.

**Quality Gate:**
- [x] Default configuration validates successfully.
- [x] Valid JSON loads into typed objects.
- [x] Save/reload preserves configured data.

---

## Phase 3: Validation

- [x] 3.1 Validate VIN format and return a clear field-specific error for invalid VIN values.
- [x] 3.2 Validate EID and GID format and return clear field-specific errors for invalid values.
- [x] 3.3 Validate logical address format/range and return a clear field-specific error for invalid values.
- [x] 3.4 Validate configured ports and return clear field-specific errors for invalid values.
- [x] 3.5 Validate source address whitelist entries and return clear field-specific errors for invalid values.

**Quality Gate:**
- [x] Invalid VIN is rejected with a clear error.
- [x] Invalid port is rejected with a clear error.
- [x] Invalid logical address is rejected with a clear error.
- [x] Validation remains limited to configuration fields.

---

## Phase 4: Tests And Scope Check

- [x] 4.1 Add unit tests for default configuration generation and validation.
- [x] 4.2 Add unit tests for valid JSON load into strongly typed objects.
- [x] 4.3 Add unit tests for invalid VIN, invalid port, and invalid logical address error clarity.
- [x] 4.4 Add unit tests for save/reload round trip preserving data.
- [x] 4.5 Confirm no YAML support, ODX/PDX import, web editing, configuration version migration, or DoIP/UDS runtime behavior was added.

**Quality Gate:**
- [x] Backend build passes.
- [x] Backend tests pass.
- [x] Scope check passes.

---

## Completion Checklist

- [x] `SimulatorConfig` is defined.
- [x] JSON configuration load is supported.
- [x] JSON configuration save is supported.
- [x] Missing configuration can produce a valid default configuration.
- [x] VIN, EID, GID, logical address, ports, and source address whitelist are validated.
- [x] DID, DTC, Routine, Session, SecurityAccess, Flash, and TLS structures are reserved as data contracts.
- [x] Default configuration passes validation.
- [x] Valid configuration loads into strongly typed objects.
- [x] Invalid VIN, invalid port, and invalid logical address return clear errors.
- [x] Save/reload preserves data.
- [x] Backend build executed.
- [x] Backend tests executed.
- [x] Scope check confirms no out-of-scope capability was added.
