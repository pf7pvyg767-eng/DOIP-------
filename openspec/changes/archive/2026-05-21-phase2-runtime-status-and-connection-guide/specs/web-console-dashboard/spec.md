## ADDED Requirements

### Requirement: Connection Guide Overview
The WebConsole dashboard SHALL display a first-screen connection guide using real runtime summary data.

#### Scenario: Display diagnostic tester connection parameters
- **GIVEN** `GET /api/runtime/summary` returns runtime connection data
- **WHEN** the WebConsole dashboard loads
- **THEN** the Overview SHALL display the Web API endpoint
- **AND** the Overview SHALL display the DoIP UDP port
- **AND** the Overview SHALL display the DoIP TCP port
- **AND** the Overview SHALL display the DoIP TLS port and TLS enabled state
- **AND** the Overview SHALL display the ECU VIN
- **AND** the Overview SHALL display the ECU logical address
- **AND** the Overview SHALL display the tester source address whitelist
- **AND** the Overview SHALL display the configuration path when available
- **AND** the Overview SHALL display the runtime start timestamp
- **AND** the Overview SHALL display the process ID
- **AND** the Overview SHALL display the active connection count

#### Scenario: Runtime summary load failure keeps dashboard usable
- **GIVEN** `GET /api/runtime/summary` fails
- **WHEN** the WebConsole dashboard handles the failure
- **THEN** the dashboard SHALL remain rendered
- **AND** the connection guide SHALL show a clear unavailable state
- **AND** existing health and configuration dashboard sections SHALL remain usable when their APIs succeed

### Requirement: Connection Guide Uses Real Data
The WebConsole connection guide SHALL use backend runtime data rather than hard-coded or mock connection values.

#### Scenario: Backend port values are reflected
- **GIVEN** the simulator is started with non-default Web API or DoIP port values
- **WHEN** the WebConsole dashboard renders the connection guide
- **THEN** the displayed port values SHALL match the values returned by `GET /api/runtime/summary`
- **AND** the displayed values SHALL NOT be hard-coded defaults

#### Scenario: Source whitelist is displayed from configuration
- **GIVEN** the simulator configuration contains one or more tester source addresses in the whitelist
- **WHEN** the WebConsole dashboard renders the connection guide
- **THEN** the displayed tester source addresses SHALL match the runtime summary response

## MODIFIED Requirements

### Requirement: 基础控制台范围限制

The WebConsole dashboard SHALL remain limited to read-only service health, configuration summary, runtime connection guidance, and high-level runtime status display.

#### Scenario: 不实现配置编辑
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT include configuration editing fields
- **AND** it SHALL NOT include save, update, or patch behavior for simulator configuration

#### Scenario: 允许连接指引但不实现诊断控制
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it MAY consume read-only runtime summary, connection snapshot, and runtime event data
- **AND** it SHALL NOT add controls that send DoIP or UDS diagnostic messages
- **AND** it SHALL NOT add configuration mutation behavior

#### Scenario: 不修改后端协议行为
- **GIVEN** this change is implemented
- **WHEN** the backend and protocol implementation are inspected
- **THEN** it SHALL NOT add new DoIP or UDS protocol behavior
- **AND** it SHALL NOT change existing Routing Activation, Vehicle Identification, UDS dispatcher, DID, DTC, Routine, Flash, TLS, PCAP, or fault injection semantics
