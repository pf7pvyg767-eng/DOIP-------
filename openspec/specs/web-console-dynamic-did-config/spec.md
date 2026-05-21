# web-console-dynamic-did-config Specification

## Purpose
TBD - created by archiving change phase2-dynamic-did-console. Update Purpose after archive.
## Requirements
### Requirement: DID Provider Update API
WebApi SHALL expose an endpoint to update the value provider for a configured DID.

#### Scenario: Update DID to sine provider
- **GIVEN** DID `0xF190` is configured as a static DID
- **WHEN** a client sends a valid sine provider update to `PUT /api/dids/0xF190/provider`
- **THEN** WebApi SHALL return HTTP `200`
- **AND** later runtime reads of DID `0xF190` SHALL use the sine provider without restarting the host

#### Scenario: Reject invalid provider update
- **GIVEN** DID `0xF190` is configured
- **WHEN** a client sends an invalid provider update
- **THEN** WebApi SHALL return HTTP `400`
- **AND** the response body SHALL include a clear validation message

#### Scenario: Unknown DID provider update returns not found
- **GIVEN** DID `0xF199` is not configured
- **WHEN** a client sends a provider update to `PUT /api/dids/0xF199/provider`
- **THEN** WebApi SHALL return HTTP `404`

### Requirement: WebConsole DID Provider Display
The WebConsole DID panel SHALL show whether each DID is static or dynamic.

#### Scenario: DID list displays provider type
- **GIVEN** WebConsole loads configured DIDs
- **WHEN** a DID has no provider or a static provider
- **THEN** the DID row SHALL show provider type `static`
- **AND** static writable DIDs SHALL keep the hex value write form

#### Scenario: Dynamic DID row displays provider parameters
- **GIVEN** WebConsole loads a DID with provider type `random`, `sine`, or `linear`
- **WHEN** the DID row is rendered
- **THEN** the row SHALL show editable fields for that provider type's parameters
- **AND** the static hex write form SHALL be disabled or hidden for the dynamic DID

### Requirement: WebConsole DID Provider Editing
The WebConsole DID panel SHALL let users submit valid dynamic provider updates and display validation failures.

#### Scenario: Submit valid sine provider
- **GIVEN** a user edits a DID provider form to valid sine parameters
- **WHEN** the user submits the provider update
- **THEN** WebConsole SHALL call the DID provider update API
- **AND** the DID list SHALL refresh after the update succeeds

#### Scenario: Display invalid provider error
- **GIVEN** a user submits invalid provider parameters
- **WHEN** WebApi returns a validation error
- **THEN** WebConsole SHALL display the error message in the DID row

