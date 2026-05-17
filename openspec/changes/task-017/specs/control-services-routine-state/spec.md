# Spec: Routine、通信控制和 DTC 设置基础服务

**Change ID:** `task-017`
**Status:** Draft

---

## ADDED Requirements

### Requirement: Routine Configuration Fixed Responses

The system SHALL support configured Routine entries with fixed responses for RoutineControl MVP handling.

#### Scenario: 加载 Routine 固定响应配置
- **GIVEN** a simulator configuration contains a Routine entry with `routineId` `0x0201`
- **AND** the entry defines fixed response payloads for `start`, `stop`, and `requestResults`
- **WHEN** the simulator runtime loads configuration
- **THEN** the Routine configuration SHALL expose the Routine ID as a 16-bit identifier
- **AND** it SHALL expose the display name when configured
- **AND** it SHALL expose the configured fixed response payloads
- **AND** it SHALL NOT require an external Routine script or ODX/PDX input

#### Scenario: 拒绝无效 Routine 配置
- **GIVEN** a simulator configuration contains an invalid Routine ID or invalid fixed response payload
- **WHEN** the configuration is validated or the Routine service is initialized
- **THEN** the operation SHALL fail with a clear error identifying the Routine field
- **AND** the simulator SHALL NOT create an ambiguous Routine entry

### Requirement: RoutineControl MVP Service

The UDS protocol layer SHALL register service `0x31` RoutineControl and return configured fixed responses for supported Routine control types.

#### Scenario: 注册 `0x31` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x31` SHALL be handled by a RoutineControl service
- **AND** unrelated service behavior SHALL remain unchanged

#### Scenario: startRoutine 返回固定响应
- **GIVEN** Routine `0x0201` is configured with a `start` fixed response payload
- **WHEN** the RoutineControl service receives a valid `0x31` startRoutine request for Routine `0x0201`
- **THEN** it SHALL return a positive `0x31` response
- **AND** the response SHALL include Routine ID `0x0201`
- **AND** the response SHALL include the configured `start` fixed response payload

#### Scenario: stopRoutine 返回固定响应
- **GIVEN** Routine `0x0201` is configured with a `stop` fixed response payload
- **WHEN** the RoutineControl service receives a valid `0x31` stopRoutine request for Routine `0x0201`
- **THEN** it SHALL return a positive `0x31` response
- **AND** the response SHALL include Routine ID `0x0201`
- **AND** the response SHALL include the configured `stop` fixed response payload

#### Scenario: requestRoutineResults 返回固定响应
- **GIVEN** Routine `0x0201` is configured with a `requestResults` fixed response payload
- **WHEN** the RoutineControl service receives a valid `0x31` requestRoutineResults request for Routine `0x0201`
- **THEN** it SHALL return a positive `0x31` response
- **AND** the response SHALL include Routine ID `0x0201`
- **AND** the response SHALL include the configured `requestResults` fixed response payload

#### Scenario: 非法 Routine ID 返回明确 NRC
- **GIVEN** Routine `0x9999` is not configured
- **WHEN** the RoutineControl service receives a `0x31` request for Routine `0x9999`
- **THEN** it SHALL return a negative response for service `0x31`
- **AND** the NRC SHALL clearly indicate request out of range or unknown Routine
- **AND** no Routine runtime script or fallback response SHALL be executed

#### Scenario: Routine 请求格式错误返回 NRC
- **GIVEN** the RoutineControl service receives a malformed request without a complete control type and Routine ID
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL clearly indicate incorrect message length or invalid format

#### Scenario: 会话或安全条件不满足返回 NRC
- **GIVEN** Routine `0x0201` declares allowed sessions or security requirement
- **AND** the current runtime session or security state does not satisfy that requirement
- **WHEN** the RoutineControl service receives a request for Routine `0x0201`
- **THEN** it SHALL return a negative response using the existing project convention for condition or security failure
- **AND** it SHALL NOT return the configured fixed success payload

### Requirement: CommunicationControl Runtime State

The system SHALL maintain a basic runtime state for UDS `0x28` CommunicationControl requests without closing real communication channels.

#### Scenario: 初始通信控制状态
- **GIVEN** the simulator runtime starts
- **WHEN** the communication control state is queried
- **THEN** the state SHALL expose a default communication mode
- **AND** it SHALL expose that no `0x28` request has changed the mode yet

#### Scenario: `0x28` 改变通信控制状态
- **GIVEN** the simulator is running
- **WHEN** the CommunicationControl service receives a supported `0x28` request
- **THEN** it SHALL update the communication control runtime state with the requested control type and communication type
- **AND** it SHALL return a positive response for service `0x28`
- **AND** subsequent state snapshots SHALL reflect the changed communication control state

#### Scenario: `0x28` 状态切换产生事件
- **GIVEN** a supported `0x28` request changes communication control state
- **WHEN** the state change is committed
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL identify service `0x28`
- **AND** the event SHALL include the control type and communication type

#### Scenario: `0x28` 不关闭真实通道
- **GIVEN** task-017 is implemented
- **WHEN** a supported `0x28` request is processed
- **THEN** the simulator SHALL NOT close active TCP, UDP, DoIP, or Web connections as part of this change
- **AND** the simulator SHALL NOT block future diagnostic requests solely because `0x28` changed the MVP state

#### Scenario: 未支持 `0x28` 参数返回明确 NRC
- **GIVEN** the CommunicationControl service receives an unsupported control type or communication type
- **WHEN** the request is validated
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL clearly indicate unsupported subfunction or request out of range
- **AND** the existing communication control state SHALL remain unchanged

### Requirement: ControlDTCSetting Runtime State

The system SHALL maintain a basic enabled/disabled runtime state for UDS `0x85` ControlDTCSetting requests without implementing full DTC setting behavior.

#### Scenario: 初始 DTC 设置状态
- **GIVEN** the simulator runtime starts
- **WHEN** the DTC setting state is queried
- **THEN** the state SHALL expose a default DTC setting mode
- **AND** it SHALL indicate that no `0x85` request has changed the mode yet

#### Scenario: `0x85` 关闭 DTC 设置
- **GIVEN** DTC setting is enabled
- **WHEN** the ControlDTCSetting service receives a supported request to disable DTC setting
- **THEN** it SHALL update the DTC setting runtime state to disabled
- **AND** it SHALL return a positive response for service `0x85`
- **AND** subsequent state snapshots SHALL reflect the disabled state

#### Scenario: `0x85` 开启 DTC 设置
- **GIVEN** DTC setting is disabled
- **WHEN** the ControlDTCSetting service receives a supported request to enable DTC setting
- **THEN** it SHALL update the DTC setting runtime state to enabled
- **AND** it SHALL return a positive response for service `0x85`
- **AND** subsequent state snapshots SHALL reflect the enabled state

#### Scenario: `0x85` 状态切换产生事件
- **GIVEN** a supported `0x85` request changes DTC setting state
- **WHEN** the state change is committed
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL identify service `0x85`
- **AND** the event SHALL include the resulting DTC setting state

#### Scenario: `0x85` 不实现完整 DTC setting 细分行为
- **GIVEN** task-017 is implemented
- **WHEN** DTC setting behavior is inspected
- **THEN** the system SHALL limit behavior to the MVP enabled/disabled runtime state
- **AND** it SHALL NOT implement full DTC status bit handling
- **AND** it SHALL NOT implement DTC storage filtering, aging, confirmation, or monitor execution logic

#### Scenario: 未支持 `0x85` 参数返回明确 NRC
- **GIVEN** the ControlDTCSetting service receives an unsupported setting type or malformed request
- **WHEN** the request is validated
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL clearly indicate unsupported subfunction, request out of range, or invalid format
- **AND** the existing DTC setting state SHALL remain unchanged

### Requirement: Web Routine And Control State Display

The WebConsole SHALL display configured Routine entries and current control service states from the simulator runtime.

#### Scenario: Web 展示 Routine 配置
- **GIVEN** the simulator configuration contains one or more Routine entries
- **WHEN** the WebConsole routine or diagnostics view loads
- **THEN** the UI SHALL display the configured Routine IDs
- **AND** it SHALL display the Routine name when available
- **AND** it SHALL display fixed-response availability for start, stop, and requestResults when available

#### Scenario: Web 展示通信控制状态
- **GIVEN** a supported `0x28` request changed communication control state
- **WHEN** the WebConsole reads the control status snapshot
- **THEN** the UI SHALL display the current communication control state
- **AND** it SHALL show the control type and communication type or equivalent readable labels

#### Scenario: Web 展示 DTC 设置状态
- **GIVEN** a supported `0x85` request changed DTC setting state
- **WHEN** the WebConsole reads the control status snapshot
- **THEN** the UI SHALL display whether DTC setting is enabled or disabled
- **AND** it SHALL reflect the latest runtime state

#### Scenario: Web 只展示状态不引入无关控制流程
- **GIVEN** task-017 is implemented
- **WHEN** the WebConsole changes are inspected
- **THEN** the UI SHALL be limited to Routine configuration display and control state display for this change
- **AND** it SHALL NOT add SecurityAccess complete-flow controls
- **AND** it SHALL NOT add Flash, ODX/PDX, PCAP/TLS, or unrelated diagnostic workflows

### Requirement: Control Services Runtime Events And Logs

The system SHALL publish runtime events or structured logs for RoutineControl calls, CommunicationControl state changes, and ControlDTCSetting state changes.

#### Scenario: Routine 调用事件进入日志
- **GIVEN** a configured Routine is called through `0x31`
- **WHEN** the service returns a positive or negative response
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL identify service `0x31`
- **AND** it SHALL include the Routine ID and operation outcome

#### Scenario: 通信控制状态事件进入日志
- **GIVEN** `0x28` changes communication control state
- **WHEN** the runtime event is published
- **THEN** the event SHALL be visible through the existing event or log path
- **AND** it SHALL include the resulting communication control state

#### Scenario: DTC 设置状态事件进入日志
- **GIVEN** `0x85` changes DTC setting state
- **WHEN** the runtime event is published
- **THEN** the event SHALL be visible through the existing event or log path
- **AND** it SHALL include the resulting DTC setting state

### Requirement: DoIP Diagnostic Integration For Control Services

The existing DoIP diagnostic forwarding path SHALL route `0x31`, `0x28`, and `0x85` requests through the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后调用 Routine
- **GIVEN** a TCP client has completed Routing Activation
- **AND** Routine `0x0201` is configured
- **WHEN** the client sends a DoIP diagnostic message with a valid UDS `0x31` payload for Routine `0x0201`
- **THEN** the payload SHALL be dispatched to the RoutineControl service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x31` response

#### Scenario: Routing Activation 后切换通信控制状态
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with a supported UDS `0x28` payload
- **THEN** the payload SHALL be dispatched to the CommunicationControl service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x28` response
- **AND** the communication control runtime state SHALL reflect the change

#### Scenario: Routing Activation 后切换 DTC 设置状态
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with a supported UDS `0x85` payload
- **THEN** the payload SHALL be dispatched to the ControlDTCSetting service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x85` response
- **AND** the DTC setting runtime state SHALL reflect the change

#### Scenario: DoIP 层不实现控制服务业务
- **GIVEN** task-017 is implemented
- **WHEN** the DoIP diagnostic message handler is inspected
- **THEN** it SHALL continue forwarding UDS payloads to the dispatcher
- **AND** it SHALL NOT parse Routine configuration directly
- **AND** it SHALL NOT mutate communication control or DTC setting state directly

### Requirement: Scope Boundaries

The implementation SHALL remain limited to RoutineControl fixed responses, CommunicationControl state, ControlDTCSetting state, Web display, and related events/logs.

#### Scenario: 不实现复杂 Routine 执行脚本
- **GIVEN** task-017 is implemented
- **WHEN** RoutineControl behavior is inspected
- **THEN** it SHALL return configured fixed responses for MVP requests
- **AND** it SHALL NOT execute scripts
- **AND** it SHALL NOT start background Routine jobs
- **AND** it SHALL NOT implement a complex Routine lifecycle beyond start, stop, and requestResults fixed responses

#### Scenario: 不实现真实通信通道关闭
- **GIVEN** task-017 is implemented
- **WHEN** CommunicationControl behavior is inspected
- **THEN** it SHALL only update simulator runtime state and events
- **AND** it SHALL NOT close sockets
- **AND** it SHALL NOT disable listeners
- **AND** it SHALL NOT block Web, DoIP, TCP, or UDP traffic

#### Scenario: 不实现完整 DTC setting 细分行为
- **GIVEN** task-017 is implemented
- **WHEN** ControlDTCSetting behavior is inspected
- **THEN** it SHALL only update the basic DTC setting state
- **AND** it SHALL NOT implement full ISO DTC setting categories or persistence rules
- **AND** it SHALL NOT modify unrelated DTC service semantics beyond the documented MVP state

#### Scenario: 不扩大到其他诊断流程
- **GIVEN** task-017 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement SecurityAccess complete flows
- **AND** it SHALL NOT implement Flash flows
- **AND** it SHALL NOT add ODX/PDX import
- **AND** it SHALL NOT add PCAP or TLS features
- **AND** it SHALL NOT perform unrelated refactoring

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
