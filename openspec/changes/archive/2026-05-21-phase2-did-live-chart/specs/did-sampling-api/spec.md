## ADDED Requirements

### Requirement: WebConsole DID Sample Consumption
The DID sampling API SHALL be suitable for repeated WebConsole polling without diagnostic tester traffic.

#### Scenario: Poll current numeric samples repeatedly
- **GIVEN** WebConsole repeatedly calls `GET /api/dids/samples`
- **WHEN** dynamic numeric DIDs are configured
- **THEN** each response SHALL contain current samples generated at request time
- **AND** the response SHALL not require a UDS `0x22` request to refresh values
