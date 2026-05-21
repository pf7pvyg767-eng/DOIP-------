## ADDED Requirements

### Requirement: Runtime Phase Summary UI
The WebConsole SHALL display a high-level runtime phase summary derived from real snapshots or runtime events.

#### Scenario: Initial phase after dashboard load
- **GIVEN** the WebConsole dashboard has loaded runtime summary and connection snapshots
- **WHEN** no active connections are reported
- **THEN** the runtime phase summary SHALL show that the API is ready and the simulator is waiting for DoIP discovery or tester connection

#### Scenario: TCP connection phase
- **GIVEN** a connection snapshot or runtime event reports an open TCP connection
- **WHEN** Routing Activation is not yet reported for that connection
- **THEN** the runtime phase summary SHALL show a TCP connected phase

#### Scenario: Routing activated phase
- **GIVEN** a connection snapshot or runtime event reports Routing Activation completed
- **WHEN** the WebConsole updates the runtime phase summary
- **THEN** the runtime phase summary SHALL show a routing activated phase
- **AND** the summary SHALL keep the related connection details available in the Diagnostics view

#### Scenario: UDS traffic active phase
- **GIVEN** the WebConsole receives a UDS request or UDS response runtime event
- **WHEN** the runtime phase summary updates
- **THEN** the runtime phase summary SHALL show that UDS traffic is active

#### Scenario: Event stream reconnect refreshes phase inputs
- **GIVEN** the WebConsole runtime event stream reconnects after a disconnect
- **WHEN** the realtime observation UI refreshes snapshots
- **THEN** the runtime phase summary SHALL recompute from the refreshed connection and ECU state snapshots
