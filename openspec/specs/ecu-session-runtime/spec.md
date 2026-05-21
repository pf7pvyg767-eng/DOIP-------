# ecu-session-runtime Specification

## Purpose
TBD - created by archiving change task-012. Update Purpose after archive.
## Requirements
### Requirement: Minimal ECU Runtime State

The system SHALL define a minimal in-memory ECU runtime state for UDS services introduced by this change.

#### Scenario: 初始化最小 ECU 状态
- **GIVEN** the simulator Host starts and UDS services are registered
- **WHEN** the ECU runtime state is created
- **THEN** the state SHALL expose the configured or effective ECU logical address
- **AND** the current diagnostic session SHALL be default session
- **AND** the security state summary SHALL remain locked
- **AND** the state SHALL NOT require SecurityAccess implementation

#### Scenario: 保存当前诊断会话
- **GIVEN** a valid DiagnosticSessionControl request changes session
- **WHEN** the service updates ECU runtime state
- **THEN** the current session SHALL be updated to the requested supported session
- **AND** later UDS services SHALL be able to observe the updated session through the shared state
- **AND** the update SHALL NOT persist to configuration files

#### Scenario: 记录 TesterPresent 时间
- **GIVEN** a valid TesterPresent request is accepted
- **WHEN** the service updates ECU runtime state
- **THEN** the state SHALL record `lastTesterPresentAt` or an equivalent timestamp
- **AND** the timestamp SHALL represent the accepted request time
- **AND** the system SHALL NOT schedule timeout fallback behavior in this change

#### Scenario: 最小状态不实现后续能力
- **GIVEN** task-012 is implemented
- **WHEN** the ECU runtime state contract is inspected
- **THEN** it SHALL NOT implement SecurityAccess seed/key state
- **AND** it SHALL NOT implement DID, DTC, Routine, or flashing execution state
- **AND** it SHALL NOT implement ResponsePending or P2/P2* timer scheduling

### Requirement: Diagnostic Session Model

The system SHALL define supported diagnostic sessions for this change.

#### Scenario: 支持默认会话
- **GIVEN** the session model is used by DiagnosticSessionControl
- **WHEN** sub-function `0x01` is requested
- **THEN** the requested session SHALL be interpreted as default session

#### Scenario: 支持编程会话
- **GIVEN** the session model is used by DiagnosticSessionControl
- **WHEN** sub-function `0x02` is requested
- **THEN** the requested session SHALL be interpreted as programming session

#### Scenario: 支持扩展会话
- **GIVEN** the session model is used by DiagnosticSessionControl
- **WHEN** sub-function `0x03` is requested
- **THEN** the requested session SHALL be interpreted as extended session

### Requirement: DiagnosticSessionControl Service

The UDS protocol layer SHALL implement DiagnosticSessionControl service `0x10` for supported session sub-functions.

#### Scenario: `10 01` 切换默认会话
- **GIVEN** a UDS dispatcher has registered the DiagnosticSessionControl service
- **WHEN** it receives UDS request bytes `0x10, 0x01`
- **THEN** the ECU runtime state SHALL switch to default session
- **AND** the service SHALL return a positive response
- **AND** the response SHALL include service ID `0x50`
- **AND** the response SHALL echo sub-function `0x01`
- **AND** the response SHALL include the configured or fixed baseline P2 and P2* parameters

#### Scenario: `10 03` 切换扩展会话
- **GIVEN** a UDS dispatcher has registered the DiagnosticSessionControl service
- **WHEN** it receives UDS request bytes `0x10, 0x03`
- **THEN** the ECU runtime state SHALL switch to extended session
- **AND** the service SHALL return a positive response
- **AND** the response SHALL include service ID `0x50`
- **AND** the response SHALL echo sub-function `0x03`
- **AND** the response SHALL include the configured or fixed baseline P2 and P2* parameters

#### Scenario: `10 02` 切换编程会话
- **GIVEN** a UDS dispatcher has registered the DiagnosticSessionControl service
- **WHEN** it receives UDS request bytes `0x10, 0x02`
- **THEN** the ECU runtime state SHALL switch to programming session
- **AND** the service SHALL return a positive response
- **AND** the response SHALL include service ID `0x50`
- **AND** the response SHALL echo sub-function `0x02`
- **AND** the response SHALL include the configured or fixed baseline P2 and P2* parameters

#### Scenario: `0x10` 请求长度错误
- **GIVEN** the DiagnosticSessionControl service receives a request without exactly one sub-function byte
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL indicate incorrect message length or invalid format
- **AND** the ECU runtime state SHALL NOT change session

#### Scenario: `0x10` 未知子功能
- **GIVEN** the DiagnosticSessionControl service receives a sub-function other than `0x01`, `0x02`, or `0x03`
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the response SHALL use the existing negative response model
- **AND** the ECU runtime state SHALL NOT change session

### Requirement: Baseline P2 And P2* Parameters

DiagnosticSessionControl positive responses SHALL include deterministic baseline P2 and P2* parameters.

#### Scenario: 返回基础 P2/P2* 参数
- **GIVEN** a supported DiagnosticSessionControl sub-function is accepted
- **WHEN** the positive response is encoded
- **THEN** the response SHALL include P2 and P2* parameter bytes after the echoed sub-function
- **AND** the selected baseline values SHALL be deterministic
- **AND** automated tests SHALL verify the exact encoded values or the project-defined equivalent representation

#### Scenario: 不启动 P2/P2* 计时器
- **GIVEN** DiagnosticSessionControl returns P2 and P2* parameters
- **WHEN** task-012 behavior is inspected
- **THEN** the implementation SHALL NOT schedule P2 timeout handling
- **AND** it SHALL NOT schedule P2* timeout handling
- **AND** it SHALL NOT emit `0x78 ResponsePending`

### Requirement: TesterPresent Service

The UDS protocol layer SHALL implement TesterPresent service `0x3E` for sub-function `0x00`.

#### Scenario: `3E 00` 返回正响应
- **GIVEN** a UDS dispatcher has registered the TesterPresent service
- **WHEN** it receives UDS request bytes `0x3E, 0x00`
- **THEN** the service SHALL return a positive response
- **AND** the encoded response bytes SHALL be `0x7E, 0x00`
- **AND** the ECU runtime state SHALL record the accepted TesterPresent time

#### Scenario: `0x3E` 请求长度错误
- **GIVEN** the TesterPresent service receives a request without exactly one sub-function byte
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL indicate incorrect message length or invalid format
- **AND** the ECU runtime state SHALL NOT update `lastTesterPresentAt`

#### Scenario: `0x3E` 未知子功能
- **GIVEN** the TesterPresent service receives a sub-function other than `0x00`
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response using the existing negative response model
- **AND** the ECU runtime state SHALL NOT update `lastTesterPresentAt`

#### Scenario: 不实现 TesterPresent 超时回退
- **GIVEN** TesterPresent service is implemented
- **WHEN** no TesterPresent request is received for a period of time
- **THEN** task-012 SHALL NOT automatically change diagnostic session
- **AND** it SHALL NOT disconnect the tester
- **AND** it SHALL NOT emit timeout fallback behavior

### Requirement: Session Change Runtime Events

The system SHALL publish structured runtime events when the diagnostic session changes.

#### Scenario: 发布默认会话变化事件
- **GIVEN** the ECU runtime state is not already in the same logical session transition context
- **WHEN** `10 01` is accepted and the session is set to default
- **THEN** the runtime event subsystem SHALL publish a session change event
- **AND** the event SHALL include the previous session
- **AND** the event SHALL include the new session `default`
- **AND** the event SHALL include logical address or connection summary when available

#### Scenario: 发布扩展会话变化事件
- **GIVEN** a supported DiagnosticSessionControl request `10 03` is accepted
- **WHEN** the session is set to extended
- **THEN** the runtime event subsystem SHALL publish a session change event
- **AND** the event SHALL include the new session `extended`
- **AND** the event SHALL be visible through existing structured logging

#### Scenario: 发布编程会话变化事件
- **GIVEN** a supported DiagnosticSessionControl request `10 02` is accepted
- **WHEN** the session is set to programming
- **THEN** the runtime event subsystem SHALL publish a session change event
- **AND** the event SHALL include the new session `programming`
- **AND** the event SHALL be visible through existing structured logging

#### Scenario: 会话未改变也不阻塞正响应
- **GIVEN** the current session already matches the requested supported session
- **WHEN** DiagnosticSessionControl receives the same supported sub-function again
- **THEN** the service SHALL still return a positive response
- **AND** the runtime state SHALL remain in the requested session
- **AND** the implementation MAY publish an event that records the accepted request or unchanged transition

### Requirement: DoIP Diagnostic Integration

The existing DoIP diagnostic forwarding path SHALL route `0x10` and `0x3E` requests through the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后处理 `10 03`
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with UDS payload `0x10, 0x03`
- **THEN** the payload SHALL be dispatched to the DiagnosticSessionControl service
- **AND** the client SHALL receive a DoIP diagnostic response containing a positive UDS response
- **AND** the ECU runtime state SHALL become extended session

#### Scenario: Routing Activation 后处理 `3E 00`
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with UDS payload `0x3E, 0x00`
- **THEN** the payload SHALL be dispatched to the TesterPresent service
- **AND** the client SHALL receive a DoIP diagnostic response containing UDS bytes `0x7E, 0x00`
- **AND** the TCP connection SHALL remain usable for later frames

### Requirement: Scope Boundaries

The implementation SHALL remain limited to minimal ECU runtime state, DiagnosticSessionControl, TesterPresent, session change events, and baseline P2/P2* response parameters.

#### Scenario: 不实现 SecurityAccess
- **GIVEN** task-012 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement seed/key generation
- **AND** it SHALL NOT implement unlock levels
- **AND** it SHALL NOT implement failed attempt counters or security delay timers

#### Scenario: 不实现 ResponsePending
- **GIVEN** task-012 is implemented
- **WHEN** DiagnosticSessionControl or TesterPresent is handled
- **THEN** the system SHALL NOT return `0x7F SID 0x78`
- **AND** it SHALL NOT schedule delayed final responses
- **AND** it SHALL NOT add pending-response queues

#### Scenario: 不实现其他 UDS 业务服务
- **GIVEN** task-012 is implemented
- **WHEN** the UDS service registry is inspected
- **THEN** this change SHALL NOT implement ReadDataByIdentifier, WriteDataByIdentifier, DTC services, RoutineControl, SecurityAccess, CommunicationControl, ControlDTCSetting, or flashing services
- **AND** unsupported services outside `0x10` and `0x3E` SHALL continue to use dispatcher negative response behavior

#### Scenario: 不新增 Web 管理能力
- **GIVEN** task-012 is implemented
- **WHEN** UI and API changes are inspected
- **THEN** it SHALL NOT add a new Web UI page
- **AND** it SHALL NOT add diagnostic session editing APIs
- **AND** it SHALL NOT add manual NRC, custom UDS response, or fault injection controls

