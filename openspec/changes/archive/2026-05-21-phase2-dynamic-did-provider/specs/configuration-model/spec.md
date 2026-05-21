## ADDED Requirements

### Requirement: DID Value Provider Configuration Model
The configuration model SHALL expose DID value provider fields as strongly typed configuration data.

#### Scenario: Load value provider from JSON
- **GIVEN** a simulator JSON configuration contains a DID with `valueProvider`
- **WHEN** the configuration subsystem loads the JSON
- **THEN** the DID configuration SHALL preserve provider fields including `type`, `numericType`, `min`, `max`, `amplitude`, `offset`, `periodMs`, `slopePerSecond`, and `seed`
- **AND** the loaded configuration SHALL keep existing static DID fields compatible

#### Scenario: Save and reload value provider
- **GIVEN** a valid `SimulatorConfig` contains DID value provider configuration
- **WHEN** the configuration subsystem saves the configuration and then reloads it
- **THEN** the provider type and numeric parameters SHALL be preserved
- **AND** the reloaded configuration SHALL pass validation

### Requirement: DID Value Provider Validation
The configuration validator SHALL validate DID value provider definitions before configuration is accepted.

#### Scenario: Static DID validation remains unchanged
- **GIVEN** a DID has no `valueProvider` or has `valueProvider.type` set to `static`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `valueEncoding` to be `hex`
- **AND** validation SHALL require `value` to be an even-length hexadecimal byte string

#### Scenario: Validate random provider
- **GIVEN** a DID has `valueProvider.type` set to `random`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `min`, and `max`
- **AND** validation SHALL reject `min` greater than `max`
- **AND** validation SHALL reject ranges that cannot fit in the selected numeric type

#### Scenario: Validate sine provider
- **GIVEN** a DID has `valueProvider.type` set to `sine`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `amplitude`, `offset`, and positive `periodMs`
- **AND** validation SHALL reject a sine output range that cannot fit in the selected numeric type

#### Scenario: Validate linear provider
- **GIVEN** a DID has `valueProvider.type` set to `linear`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `offset`, and `slopePerSecond`
- **AND** validation SHALL reject unsupported `numericType` values

#### Scenario: Reject writable dynamic DID
- **GIVEN** a DID has a dynamic provider type
- **AND** the DID is marked writable
- **WHEN** configuration validation runs
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify that dynamic provider DIDs are read-only for this change
