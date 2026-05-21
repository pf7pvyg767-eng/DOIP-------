## ADDED Requirements

### Requirement: Runtime Shutdown API
The WebApi SHALL expose a controlled runtime shutdown endpoint.

#### Scenario: Request runtime shutdown
- **GIVEN** the simulator Host is running WebApi
- **WHEN** a client sends `POST /api/runtime/shutdown`
- **THEN** the WebApi SHALL accept the shutdown request
- **AND** the WebApi SHALL trigger the Host runtime shutdown signal
- **AND** the endpoint SHALL NOT require a request body

#### Scenario: Publish shutdown requested event
- **GIVEN** the simulator Host is running with runtime events enabled
- **WHEN** `POST /api/runtime/shutdown` is accepted
- **THEN** the system SHALL publish a `system.shutdown.requested` runtime event
- **AND** the event SHALL use the `system` category
- **AND** the event SHALL be available to the in-memory event stream before shutdown begins when the stream is still connected

#### Scenario: Repeat shutdown request while stopping
- **GIVEN** a shutdown request has already been accepted
- **WHEN** another shutdown request reaches the WebApi before the process exits
- **THEN** the request SHALL NOT start a second independent shutdown sequence
- **AND** the WebApi SHALL return a successful or accepted result when it is still able to respond

### Requirement: Host Controlled Shutdown
The Host SHALL stop the full simulator runtime when the WebApi shutdown signal is triggered.

#### Scenario: Stop runtime from API request
- **GIVEN** the Host is running WebApi and DoIP listeners
- **WHEN** the WebApi shutdown endpoint triggers the runtime shutdown signal
- **THEN** the Host SHALL stop WebApi
- **AND** the Host SHALL stop UDP DoIP, TCP DoIP, and TLS DoIP listeners that are running
- **AND** the Host process SHALL exit successfully

#### Scenario: Release runtime ports after shutdown
- **GIVEN** the Host is running WebApi and DoIP listeners on configured ports
- **WHEN** runtime shutdown completes
- **THEN** the WebApi port SHALL be released
- **AND** the UDP DoIP port SHALL be released
- **AND** the TCP DoIP port SHALL be released
- **AND** the TLS DoIP port SHALL be released when TLS was running

#### Scenario: Preserve Ctrl+C shutdown behavior
- **GIVEN** the Host supports graceful Ctrl+C shutdown
- **WHEN** WebApi runtime shutdown control is added
- **THEN** Ctrl+C shutdown SHALL continue to stop the runtime gracefully
- **AND** Ctrl+C shutdown SHALL continue to release the bound ports

### Requirement: Shutdown Resource Cleanup
The runtime shutdown path SHALL clean up active runtime resources before process exit where practical.

#### Scenario: Stop active pcap recording during shutdown
- **GIVEN** pcap recording is active
- **WHEN** a WebApi runtime shutdown request is accepted
- **THEN** the system SHALL stop the active pcap recording before triggering final Host shutdown
- **AND** the pcap recorder SHALL flush and close the active pcap file
- **AND** the pcap stop behavior SHALL remain compatible with the existing PCAP recorder lifecycle

#### Scenario: Continue shutdown when pcap is inactive
- **GIVEN** pcap recording is not active
- **WHEN** a WebApi runtime shutdown request is accepted
- **THEN** the system SHALL continue the shutdown sequence without attempting to create or start a pcap recording session

### Requirement: Shutdown Scope Boundaries
Runtime shutdown control SHALL be limited to stopping the current simulator process.

#### Scenario: No diagnostic protocol mutation
- **GIVEN** runtime shutdown control is implemented
- **WHEN** the backend protocol implementation is inspected
- **THEN** the change SHALL NOT add new DoIP diagnostic messages
- **AND** the change SHALL NOT add new UDS service behavior
- **AND** the change SHALL NOT change DID, DTC, Routine, Flash, TLS, PCAP packet content, or fault injection semantics except for orderly shutdown cleanup

#### Scenario: No restart or supervisor behavior
- **GIVEN** runtime shutdown has completed
- **WHEN** the Host process exits
- **THEN** the system SHALL NOT automatically restart the simulator
- **AND** a user or external supervisor MUST start a new Host process for the next run
