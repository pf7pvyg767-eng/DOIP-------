## ADDED Requirements

### Requirement: DID Read Event Sample Data
Successful ReadDataByIdentifier events SHALL include current DID sample data for each returned DID.

#### Scenario: Dynamic DID read event includes numeric sample
- **GIVEN** DID `0xF192` is configured with a valid dynamic numeric value provider
- **WHEN** the ReadDataByIdentifier service successfully reads DID `0xF192`
- **THEN** the published `uds.did.read` event SHALL include the DID identifier
- **AND** the event data SHALL include the raw value as uppercase hex
- **AND** the event data SHALL include the decoded numeric value
- **AND** the event data SHALL include the provider type
- **AND** the event data SHALL include the sampled timestamp
- **AND** the event data SHALL include the connection ID when available

#### Scenario: Static DID read event includes raw sample
- **GIVEN** DID `0xF190` is configured with fixed raw hex bytes and no dynamic numeric provider
- **WHEN** the ReadDataByIdentifier service successfully reads DID `0xF190`
- **THEN** the published `uds.did.read` event SHALL include the raw value as uppercase hex
- **AND** the event data SHALL NOT include a numeric value
- **AND** the provider type SHALL be `static`

#### Scenario: Rejected DID read does not publish sample event
- **GIVEN** a ReadDataByIdentifier request is rejected for invalid format, security access, or unconfigured DID
- **WHEN** the service returns a negative response
- **THEN** it SHALL NOT publish `uds.did.read` sample data for the rejected DID
