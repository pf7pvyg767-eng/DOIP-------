## ADDED Requirements

### Requirement: DID Value Provider Configuration
The simulator configuration SHALL support an optional DID `valueProvider` object for generated DID values.

#### Scenario: Static DID remains default
- **GIVEN** a DID configuration has no `valueProvider`
- **WHEN** the configuration is loaded
- **THEN** the DID SHALL be treated as a static DID
- **AND** the existing `valueEncoding` and `value` fields SHALL continue to define its response bytes

#### Scenario: Explicit static provider uses fixed value
- **GIVEN** a DID configuration has `valueProvider.type` set to `static`
- **WHEN** the configuration is loaded
- **THEN** the DID SHALL use the configured fixed hex value
- **AND** the behavior SHALL match a DID with no `valueProvider`

#### Scenario: Dynamic provider does not require fixed value
- **GIVEN** a DID configuration has `valueProvider.type` set to `random`, `sine`, or `linear`
- **WHEN** the configuration is validated
- **THEN** the DID SHALL NOT require a fixed `value` field for read responses
- **AND** the generated value SHALL be encoded from the provider configuration

### Requirement: Dynamic DID Numeric Encoding
Dynamic DID providers SHALL encode generated numeric samples to deterministic response bytes.

#### Scenario: Encode supported numeric types
- **GIVEN** a dynamic DID provider has `numericType` set to `uint8`, `uint16`, `int16`, `uint32`, or `int32`
- **WHEN** the provider generates a numeric sample
- **THEN** the sample SHALL be encoded to the byte length defined by the numeric type
- **AND** multi-byte numeric values SHALL use big-endian byte order

#### Scenario: Generated value length follows numeric type
- **GIVEN** a dynamic DID provider has a supported `numericType`
- **WHEN** `DidRuntimeStore` returns the current DID value
- **THEN** the returned byte array length SHALL equal the encoded length of the numeric type

#### Scenario: Reject unsupported numeric type
- **GIVEN** a dynamic DID provider has an unsupported `numericType`
- **WHEN** configuration validation runs
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the DID provider numeric type field

### Requirement: Random DID Provider
The random DID provider SHALL generate values within a configured numeric range.

#### Scenario: Validate random provider fields
- **GIVEN** a DID configuration has `valueProvider.type` set to `random`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `min`, and `max`
- **AND** validation SHALL require `min` to be less than or equal to `max`
- **AND** validation SHALL require the range to fit within the selected numeric type

#### Scenario: Read random DID value
- **GIVEN** a DID is configured with a valid random provider
- **WHEN** the DID is read through the runtime store
- **THEN** the generated numeric value SHALL be within the configured `min` and `max` range
- **AND** the returned bytes SHALL match the configured `numericType`

#### Scenario: Seeded random provider is repeatable
- **GIVEN** two simulator runtimes start with the same random provider configuration and the same `seed`
- **WHEN** each runtime reads the DID in the same sequence
- **THEN** the generated value sequence SHALL be repeatable for that seed

### Requirement: Sine DID Provider
The sine DID provider SHALL generate a periodic numeric value from configured amplitude, offset, and period.

#### Scenario: Validate sine provider fields
- **GIVEN** a DID configuration has `valueProvider.type` set to `sine`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `amplitude`, `offset`, and `periodMs`
- **AND** validation SHALL require `periodMs` to be greater than zero
- **AND** validation SHALL require the configured output range to fit within the selected numeric type

#### Scenario: Read sine DID value
- **GIVEN** a DID is configured with a valid sine provider
- **WHEN** the DID is read through the runtime store
- **THEN** the generated numeric value SHALL follow the configured sine wave based on elapsed runtime time
- **AND** the returned bytes SHALL match the configured `numericType`

### Requirement: Linear DID Provider
The linear DID provider SHALL generate a numeric value from configured offset and slope.

#### Scenario: Validate linear provider fields
- **GIVEN** a DID configuration has `valueProvider.type` set to `linear`
- **WHEN** configuration validation runs
- **THEN** validation SHALL require `numericType`, `offset`, and `slopePerSecond`
- **AND** validation SHALL require the generated value encoding to be compatible with the selected numeric type

#### Scenario: Read linear DID value
- **GIVEN** a DID is configured with a valid linear provider
- **WHEN** the DID is read through the runtime store after elapsed runtime time
- **THEN** the generated numeric value SHALL equal `offset + slopePerSecond * elapsedSeconds` before encoding and clamping
- **AND** the returned bytes SHALL match the configured `numericType`

### Requirement: Dynamic DID Provider Boundaries
Dynamic DID provider support SHALL remain limited to built-in numeric providers.

#### Scenario: No scripts or expression execution
- **GIVEN** dynamic DID providers are implemented
- **WHEN** DID configuration is inspected
- **THEN** the system SHALL NOT execute script code from configuration
- **AND** the system SHALL NOT evaluate arbitrary expression strings

#### Scenario: No chart UI in provider model task
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** the change SHALL NOT add realtime DID chart controls
- **AND** it SHALL NOT add a provider editing UI
