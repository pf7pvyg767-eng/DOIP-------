# Spec: Configuration Model, Default Configuration, And Validation

**Change ID:** `task-003`
**Status:** Draft

---

## ADDED Requirements

### Requirement: Strongly Typed Simulator Configuration

The Core project SHALL define a strongly typed `SimulatorConfig` model for the simulator configuration contract.

#### Scenario: Represent simulator identity and network settings
- **GIVEN** the configuration contract is used by Host, WebApi, or future protocol modules
- **WHEN** code reads the simulator configuration
- **THEN** the configuration SHALL expose strongly typed simulator identity settings including VIN, EID, GID, and logical address
- **AND** the configuration SHALL expose strongly typed network settings including bind address, DoIP UDP port, DoIP TCP port, DoIP TLS port, and source address whitelist

#### Scenario: Reserve future configuration sections
- **GIVEN** the configuration contract is extended for later tasks
- **WHEN** `SimulatorConfig` is defined
- **THEN** it SHALL include reserved typed configuration structures for DID, DTC, Routine, Session, SecurityAccess, Flash, and TLS
- **AND** those structures SHALL remain data contracts only
- **AND** they SHALL NOT implement DoIP, UDS, SecurityAccess, Flash, TLS runtime behavior, or ODX/PDX import

### Requirement: Default Configuration Generation

The configuration subsystem SHALL provide a default `SimulatorConfig` when configuration is missing.

#### Scenario: Generate default configuration
- **GIVEN** no simulator configuration is available at the requested configuration location
- **WHEN** the configuration subsystem handles the missing configuration
- **THEN** it SHALL provide a default `SimulatorConfig`
- **AND** the default configuration SHALL contain a valid VIN, EID, GID, logical address, port values, and source address whitelist
- **AND** the default configuration SHALL pass configuration validation

### Requirement: JSON Configuration Load

The configuration subsystem SHALL load simulator configuration from JSON into strongly typed objects.

#### Scenario: Load valid JSON configuration
- **GIVEN** a valid JSON simulator configuration exists
- **WHEN** the configuration subsystem loads the JSON configuration
- **THEN** it SHALL deserialize the content into `SimulatorConfig`
- **AND** it SHALL preserve the configured identity, network, UDS placeholder, and TLS values in strongly typed properties
- **AND** the loaded configuration SHALL pass validation

### Requirement: JSON Configuration Save

The configuration subsystem SHALL save `SimulatorConfig` as JSON without losing configured data.

#### Scenario: Save and reload configuration
- **GIVEN** a valid `SimulatorConfig` contains non-default identity, network, UDS placeholder, or TLS values
- **WHEN** the configuration subsystem saves the configuration to JSON and then reloads it
- **THEN** the reloaded `SimulatorConfig` SHALL preserve the saved data
- **AND** the reloaded configuration SHALL pass validation

### Requirement: Configuration Field Validation

The configuration validator SHALL validate VIN, EID, GID, logical address, port, and source address whitelist fields before configuration is accepted.

#### Scenario: Reject invalid VIN
- **GIVEN** a simulator configuration contains an invalid VIN
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL include a clear error identifying the VIN field

#### Scenario: Reject invalid EID or GID
- **GIVEN** a simulator configuration contains an invalid EID or invalid GID
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL include a clear error identifying the invalid EID or GID field

#### Scenario: Reject invalid logical address
- **GIVEN** a simulator configuration contains an invalid logical address
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL include a clear error identifying the logical address field

#### Scenario: Reject invalid port
- **GIVEN** a simulator configuration contains an invalid configured port
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL include a clear error identifying the invalid port field

#### Scenario: Reject invalid source address whitelist entry
- **GIVEN** a simulator configuration contains an invalid source address whitelist entry
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL include a clear error identifying the source address whitelist field

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
