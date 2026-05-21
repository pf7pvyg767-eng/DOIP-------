# runtime-startup Specification

## Purpose
TBD - created by archiving change task-002. Update Purpose after archive.
## Requirements
### Requirement: Host Starts WebApi Runtime

The Host SHALL start the WebApi runtime when the runtime command path is executed.

#### Scenario: Start WebApi from Host
- **GIVEN** the backend solution from `task-001` is available
- **WHEN** a developer runs the Host runtime command on a free loopback port
- **THEN** the Host SHALL start WebApi
- **AND** the Host SHALL bind WebApi to the configured listen address and port
- **AND** the Host SHALL NOT start DoIP, UDS, DID, DTC, Flash, TLS, PCAP, database, or full ECU configuration behavior

### Requirement: Configurable WebApi Listen Endpoint

The Host SHALL provide minimal runtime options for configuring the WebApi listen address and port.

#### Scenario: Configure listen endpoint
- **GIVEN** a developer provides a listen address and port through the documented Host runtime options
- **WHEN** the Host starts WebApi
- **THEN** WebApi SHALL listen on the configured address and port

#### Scenario: Reject invalid listen options
- **GIVEN** a developer provides an invalid listen address or invalid port
- **WHEN** the Host validates runtime options
- **THEN** startup SHALL fail with clear command-line feedback

### Requirement: Startup URL Output

The Host SHALL print the reachable local Web console/API URL after successful startup.

#### Scenario: Print startup URL
- **GIVEN** WebApi has started successfully on port `{port}`
- **WHEN** startup information is written to the console
- **THEN** the console output SHALL include `http://127.0.0.1:{port}`

### Requirement: Port Occupancy Check

The Host SHALL detect an occupied requested port before starting WebApi.

#### Scenario: Requested port is occupied
- **GIVEN** another process is already listening on the requested port
- **WHEN** the Host runtime command is started with that port
- **THEN** startup SHALL fail
- **AND** the failure message SHALL include the occupied port number
- **AND** no partially running WebApi instance SHALL remain

### Requirement: Graceful Ctrl+C Shutdown

The Host SHALL support graceful shutdown when interrupted by Ctrl+C.

#### Scenario: Stop runtime with Ctrl+C
- **GIVEN** the Host is running WebApi on a loopback port
- **WHEN** the process receives a Ctrl+C interrupt
- **THEN** the Host SHALL stop gracefully
- **AND** the process SHALL exit
- **AND** the bound port SHALL be released

### Requirement: Health Endpoint

The WebApi SHALL expose a minimal health endpoint.

#### Scenario: Query health endpoint
- **GIVEN** WebApi is running
- **WHEN** a client sends `GET /api/health`
- **THEN** WebApi SHALL return HTTP 200
- **AND** the response SHALL include minimal health information
- **AND** the response SHALL include `status` with value `ok`

