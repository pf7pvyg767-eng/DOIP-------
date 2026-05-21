# runtime-status-summary Specification

## Purpose
TBD - created by archiving change phase2-runtime-status-and-connection-guide. Update Purpose after archive.
## Requirements
### Requirement: Runtime Summary API
The WebApi SHALL expose a read-only runtime summary endpoint for Web Console connection guidance.

#### Scenario: Query runtime summary
- **GIVEN** the simulator runtime is running
- **WHEN** a client sends `GET /api/runtime/summary`
- **THEN** WebApi SHALL return HTTP `200`
- **AND** the response SHALL include the Web API listen address
- **AND** the response SHALL include the Web API port
- **AND** the response SHALL include the DoIP UDP port
- **AND** the response SHALL include the DoIP TCP port
- **AND** the response SHALL include the DoIP TLS port
- **AND** the response SHALL include whether TLS is enabled
- **AND** the response SHALL include the ECU VIN
- **AND** the response SHALL include the ECU logical address
- **AND** the response SHALL include the tester source address whitelist
- **AND** the response SHALL include the configuration path when one is available
- **AND** the response SHALL include the runtime start timestamp
- **AND** the response SHALL include the current process ID
- **AND** the response SHALL include the active connection count

#### Scenario: Runtime summary is read-only
- **GIVEN** the simulator runtime has active connections and loaded configuration
- **WHEN** a client sends `GET /api/runtime/summary`
- **THEN** WebApi SHALL NOT mutate configuration
- **AND** WebApi SHALL NOT open or close any DoIP connection
- **AND** WebApi SHALL NOT change ECU session, security, DID, DTC, PCAP, fault, TLS, or routing activation state

#### Scenario: Runtime summary reflects current connection count
- **GIVEN** at least one client is connected to the simulator
- **WHEN** a client sends `GET /api/runtime/summary`
- **THEN** the response active connection count SHALL match the current connection registry snapshot

### Requirement: Runtime Summary Field Availability
The runtime summary endpoint SHALL provide stable fallback values for optional fields so the Web Console can render a usable connection guide.

#### Scenario: Configuration path is not provided
- **GIVEN** the simulator was started without an explicit configuration path
- **WHEN** a client sends `GET /api/runtime/summary`
- **THEN** the response SHALL still return HTTP `200`
- **AND** the configuration path field SHALL be null, empty, or a documented default path value
- **AND** all required connection fields SHALL remain present

#### Scenario: TLS is disabled
- **GIVEN** TLS is disabled in the simulator configuration
- **WHEN** a client sends `GET /api/runtime/summary`
- **THEN** the response SHALL include `tlsEnabled` as false
- **AND** the response SHALL still include the configured DoIP TLS port for visibility

