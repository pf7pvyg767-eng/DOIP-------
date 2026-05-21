## ADDED Requirements

### Requirement: Dashboard Runtime Shutdown Control
The WebConsole dashboard SHALL provide a controlled runtime shutdown action.

#### Scenario: Show shutdown action in overview
- **GIVEN** the WebConsole dashboard has loaded successfully
- **WHEN** the user views the runtime overview or service status area
- **THEN** the dashboard SHALL display a shutdown action for stopping the current simulator runtime
- **AND** the action SHALL be visually distinct from read-only status fields

#### Scenario: Require confirmation before shutdown
- **GIVEN** the shutdown action is visible
- **WHEN** the user activates the shutdown action
- **THEN** the WebConsole SHALL show a confirmation prompt or dialog before calling the backend shutdown API
- **AND** cancelling the confirmation SHALL NOT call `POST /api/runtime/shutdown`

#### Scenario: Confirm shutdown request
- **GIVEN** the confirmation prompt is visible
- **WHEN** the user confirms shutdown
- **THEN** the WebConsole SHALL call `POST /api/runtime/shutdown`
- **AND** the WebConsole SHALL enter a stopping state
- **AND** the WebConsole SHALL disable repeated shutdown submissions while stopping

#### Scenario: Display disconnected state after shutdown
- **GIVEN** a shutdown request has been confirmed
- **WHEN** subsequent WebApi requests fail because the runtime has stopped
- **THEN** the WebConsole SHALL display a clear stopped or disconnected state
- **AND** the page SHALL remain rendered rather than becoming blank
- **AND** the disconnected state SHALL NOT be presented as an unexpected dashboard load failure

#### Scenario: Show shutdown failure
- **GIVEN** the user confirms shutdown
- **WHEN** `POST /api/runtime/shutdown` fails before shutdown is accepted
- **THEN** the WebConsole SHALL display a clear failure state
- **AND** the shutdown action SHALL become available again unless the runtime is already disconnected

## MODIFIED Requirements

### Requirement: 基础控制台范围限制
The WebConsole dashboard SHALL remain limited to service health, configuration summary, runtime connection guidance, high-level runtime status display, and controlled runtime shutdown.

#### Scenario: 不实现配置编辑
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT include configuration editing fields
- **AND** it SHALL NOT include save, update, or patch behavior for simulator configuration

#### Scenario: 允许连接指引和运行时停止但不实现诊断控制
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it MAY consume read-only runtime summary, connection snapshot, and runtime event data
- **AND** it MAY send a controlled runtime shutdown request through `POST /api/runtime/shutdown`
- **AND** it SHALL NOT add controls that send DoIP or UDS diagnostic messages
- **AND** it SHALL NOT add configuration mutation behavior

#### Scenario: 不修改后端协议行为
- **GIVEN** this change is implemented
- **WHEN** the backend and protocol implementation are inspected
- **THEN** it SHALL NOT add new DoIP or UDS protocol behavior
- **AND** it SHALL NOT change existing Routing Activation, Vehicle Identification, UDS dispatcher, DID, DTC, Routine, Flash, TLS, PCAP packet content, or fault injection semantics except for orderly runtime shutdown cleanup
