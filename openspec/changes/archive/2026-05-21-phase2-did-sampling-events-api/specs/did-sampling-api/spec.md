## ADDED Requirements

### Requirement: DID Sample Contract
The system SHALL expose current DID samples with raw bytes and optional numeric metadata.

#### Scenario: Dynamic numeric DID sample
- **GIVEN** a DID is configured with a valid dynamic numeric value provider
- **WHEN** the DID is sampled
- **THEN** the sample SHALL include the DID identifier
- **AND** the sample SHALL include the raw value as uppercase hex
- **AND** the sample SHALL include the decoded numeric value
- **AND** the sample SHALL include the provider type
- **AND** the sample SHALL include the sampled timestamp

#### Scenario: Static non-numeric DID sample
- **GIVEN** a DID is configured with fixed raw hex bytes and no dynamic numeric provider
- **WHEN** the DID is sampled
- **THEN** the sample SHALL include the DID identifier
- **AND** the sample SHALL include the raw value as uppercase hex
- **AND** the sample SHALL NOT include a numeric value
- **AND** the sample SHALL identify the provider type as `static`

### Requirement: Single DID Sample API
WebApi SHALL expose `GET /api/dids/{did}/sample` to return the current sample for one configured DID.

#### Scenario: Sample configured dynamic DID
- **GIVEN** DID `0xF192` is configured with a valid dynamic numeric value provider
- **WHEN** a client requests `GET /api/dids/0xF192/sample`
- **THEN** WebApi SHALL return HTTP `200`
- **AND** the response body SHALL contain the current DID sample
- **AND** the sample SHALL be calculated without requiring a diagnostic tester request

#### Scenario: Unknown DID sample returns not found
- **GIVEN** DID `0xF199` is not configured
- **WHEN** a client requests `GET /api/dids/0xF199/sample`
- **THEN** WebApi SHALL return HTTP `404`

#### Scenario: Invalid DID sample request returns bad request
- **GIVEN** the DID route value is not a valid 16-bit DID identifier
- **WHEN** a client requests the single DID sample API
- **THEN** WebApi SHALL return HTTP `400`

### Requirement: All DID Samples API
WebApi SHALL expose `GET /api/dids/samples` to return current samples for all configured readable DIDs.

#### Scenario: Sample all configured DIDs
- **GIVEN** the simulator has static and dynamic DIDs configured
- **WHEN** a client requests `GET /api/dids/samples`
- **THEN** WebApi SHALL return HTTP `200`
- **AND** the response body SHALL contain one current sample per configured readable DID
- **AND** dynamic DID samples SHALL be calculated at request time

#### Scenario: All samples do not require diagnostic traffic
- **GIVEN** no diagnostic tester is connected
- **WHEN** a client requests `GET /api/dids/samples`
- **THEN** WebApi SHALL still return samples for configured DIDs
