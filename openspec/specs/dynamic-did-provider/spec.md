# dynamic-did-provider Specification

## Purpose
TBD - created by archiving change phase2-dynamic-did-provider. Update Purpose after archive.
## Requirements
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

### Requirement: Runtime DID Provider Evaluation
The runtime store SHALL resolve DID values at read time, using static bytes for static DIDs and generated bytes for configured dynamic providers.

#### Scenario: Static DID returns current fixed runtime value
- **GIVEN** a DID is configured without a dynamic value provider
- **WHEN** the DID is read through `DidRuntimeStore`
- **THEN** the returned bytes SHALL equal the current fixed runtime value for that DID

#### Scenario: Sine DID changes with elapsed time
- **GIVEN** a DID is configured with a valid sine provider
- **AND** the runtime time source is advanced from one sample point to another sample point with a different sine output
- **WHEN** the same DID is read at both sample points through `DidRuntimeStore`
- **THEN** both reads SHALL return valid encoded values for the provider numeric type
- **AND** the second read value SHALL differ from the first read value

#### Scenario: Linear DID changes with elapsed time
- **GIVEN** a DID is configured with a valid linear provider with a non-zero slope
- **AND** the runtime time source is advanced after the first read
- **WHEN** the same DID is read again through `DidRuntimeStore`
- **THEN** the second generated numeric value SHALL reflect the elapsed time calculation
- **AND** the second read value SHALL differ from the first read value when the elapsed time changes the encoded result

#### Scenario: Random DID remains inside configured range
- **GIVEN** a DID is configured with a valid random provider
- **WHEN** the DID is read repeatedly through `DidRuntimeStore`
- **THEN** each generated numeric value SHALL be within the configured `min` and `max` range
- **AND** each returned byte array SHALL match the configured numeric type length and byte order

#### Scenario: Runtime calculation tests can control time
- **GIVEN** a dynamic DID provider depends on elapsed runtime time
- **WHEN** automated tests instantiate the runtime store with a controlled time source
- **THEN** reads SHALL be calculated from that controlled time source
- **AND** the tests SHALL NOT require wall-clock sleeps to verify sine or linear value changes

### Requirement: Runtime DID Provider Updates
Configured DID value providers SHALL be updateable at runtime through a validated application path.

#### Scenario: Provider update affects immediate runtime reads
- **GIVEN** a configured static DID is updated to a valid dynamic provider
- **WHEN** the DID is read through `DidRuntimeStore` after the update
- **THEN** the returned value SHALL be generated by the new provider
- **AND** the runtime SHALL NOT require host restart

#### Scenario: Invalid provider update leaves current runtime value unchanged
- **GIVEN** a configured DID has a current readable value
- **WHEN** an invalid provider update is rejected
- **THEN** later reads of that DID SHALL still return values from the previous configuration

