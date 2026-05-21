# Spec: UDS 分发框架和 NRC 响应模型

**Change ID:** `task-011`
**Status:** Draft

---

## ADDED Requirements

### Requirement: UDS Request Contract

The UDS protocol layer SHALL define a request contract that represents an inbound UDS service request.

#### Scenario: 从有效 payload 创建 UDS request
- **GIVEN** an inbound UDS payload contains at least one byte
- **WHEN** the UDS layer creates a request
- **THEN** the request SHALL expose the first byte as `ServiceId`
- **AND** the request SHALL expose remaining bytes as request payload data
- **AND** the original service ID SHALL be available for response generation

#### Scenario: 空 payload 不能形成有效 request
- **GIVEN** an inbound UDS payload is empty
- **WHEN** the UDS layer attempts to create or dispatch the request
- **THEN** the system SHALL treat the input as incorrect message length or invalid format
- **AND** it SHALL produce a negative response using NRC `0x13` when a response can be sent
- **AND** it SHALL NOT crash the TCP DoIP connection

### Requirement: UDS Response And Negative Response Contract

The UDS protocol layer SHALL define a response contract with a standard byte encoding path and a negative response model.

#### Scenario: NegativeResponse 编码
- **GIVEN** a negative response is created for original SID `0x22` with NRC `0x11`
- **WHEN** the response is encoded to bytes
- **THEN** the encoded bytes SHALL be `0x7F, 0x22, 0x11`

#### Scenario: 基础 NRC 常量
- **GIVEN** the implementation defines NRC values for this change
- **WHEN** UDS dispatcher and DoIP diagnostic forwarding use NRCs
- **THEN** the implementation SHALL include `ServiceNotSupported` with value `0x11`
- **AND** it SHALL include `IncorrectMessageLengthOrInvalidFormat` with value `0x13`
- **AND** those values SHALL be used consistently by negative responses

#### Scenario: 响应统一编码
- **GIVEN** any `UdsResponse` produced by the dispatcher
- **WHEN** the DoIP diagnostic message handler needs to send the response
- **THEN** the response SHALL provide a byte representation through `ToBytes()` or an equivalent single encoding contract
- **AND** the DoIP layer SHALL NOT reimplement response-specific UDS encoding logic

### Requirement: UDS Service Registration

The UDS dispatcher SHALL support service handlers registered by service ID.

#### Scenario: 注册服务处理器
- **GIVEN** an `IUdsService` or equivalent service handler is registered for SID `0x22`
- **WHEN** the dispatcher receives a request with `ServiceId` `0x22`
- **THEN** the dispatcher SHALL invoke the registered handler
- **AND** it SHALL pass the `UdsRequest`
- **AND** it SHALL pass the `UdsContext` or equivalent minimal context
- **AND** it SHALL return the handler responses in their original order

#### Scenario: 未注册 SID 返回 ServiceNotSupported
- **GIVEN** no service handler is registered for SID `0x99`
- **WHEN** the dispatcher receives a request with `ServiceId` `0x99`
- **THEN** the dispatcher SHALL return a `NegativeResponse`
- **AND** the negative response original SID SHALL be `0x99`
- **AND** the NRC SHALL be `0x11`
- **AND** the encoded response bytes SHALL be `0x7F, 0x99, 0x11`

#### Scenario: Dispatcher 不实现正响应业务
- **GIVEN** the dispatcher is implemented
- **WHEN** no external service handler is registered for a SID
- **THEN** the dispatcher SHALL NOT synthesize a positive UDS business response
- **AND** it SHALL NOT implement DiagnosticSessionControl, TesterPresent, DID, DTC, Routine, SecurityAccess, or flashing behavior
- **AND** unsupported services SHALL use the configured default negative response behavior

### Requirement: Minimal UDS Context

The UDS dispatcher SHALL provide only the minimal context needed for service dispatch in this change.

#### Scenario: Context 传递连接和地址摘要
- **GIVEN** a DoIP diagnostic message is dispatched to UDS
- **WHEN** the dispatcher invokes a service handler
- **THEN** the context SHALL make available the connection or diagnostic routing summary needed by handlers
- **AND** it MAY include tester and ECU logical addresses when available from DoIP
- **AND** it SHALL NOT require an ECU state machine to exist

#### Scenario: Context 不实现 ECU 状态机
- **GIVEN** task-011 is implemented
- **WHEN** the context contract is inspected
- **THEN** it SHALL NOT implement session transitions
- **AND** it SHALL NOT implement security unlock state
- **AND** it SHALL NOT implement P2/P2* timing, TesterPresent timeout, or flashing phase state

### Requirement: DoIP Diagnostic Message Forwarding

The DoIP TCP diagnostic message handling path SHALL forward UDS payloads to the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后转发 diagnostic payload
- **GIVEN** a TCP connection has completed Routing Activation
- **AND** the connection receives a DoIP diagnostic message containing a UDS payload
- **WHEN** the diagnostic message handler processes the frame
- **THEN** the handler SHALL pass the UDS payload to the UDS dispatcher
- **AND** the handler SHALL preserve the diagnostic addressing context needed for a response
- **AND** the handler SHALL NOT bypass the dispatcher for service-specific behavior

#### Scenario: 包装 UDS response 为 DoIP diagnostic response
- **GIVEN** the UDS dispatcher returns one or more `UdsResponse` values
- **WHEN** the DoIP diagnostic message handler sends responses
- **THEN** each UDS response SHALL be encoded to bytes
- **AND** each encoded UDS response SHALL be placed in a DoIP diagnostic message response payload
- **AND** the response SHALL be sent on the same TCP connection

#### Scenario: 未知 SID 集成响应
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with UDS SID `0x99`
- **THEN** the client SHALL receive a DoIP diagnostic message response
- **AND** the UDS payload in the response SHALL be `0x7F, 0x99, 0x11`

#### Scenario: 空 UDS payload 集成响应
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with an empty UDS payload
- **THEN** the system SHALL return or log an incorrect length/format result using NRC `0x13` when a response can be encoded
- **AND** the TCP server SHALL continue running
- **AND** the implementation SHALL NOT generate a positive UDS response

### Requirement: UDS Runtime Events

The system SHALL publish structured runtime events for UDS request, response, and error handling through the existing event pipeline.

#### Scenario: 发布 UDS request 事件
- **GIVEN** a UDS request is accepted for dispatch
- **WHEN** the dispatcher begins handling the request
- **THEN** the runtime event subsystem SHALL publish an event representing the UDS request
- **AND** the event SHALL include the SID
- **AND** the event SHALL include a connection ID, logical address summary, or equivalent routing context when available
- **AND** the event SHALL NOT require dumping the full raw diagnostic payload

#### Scenario: 发布 UDS response 事件
- **GIVEN** the dispatcher produces a UDS response
- **WHEN** the response is returned to the DoIP diagnostic handler
- **THEN** the runtime event subsystem SHALL publish an event representing the UDS response
- **AND** the event SHALL include whether the response is negative or positive
- **AND** negative response events SHALL include the original SID and NRC

#### Scenario: 发布 UDS 错误事件
- **GIVEN** the UDS dispatcher detects incorrect length, invalid format, or an unsupported SID
- **WHEN** the condition is handled
- **THEN** the runtime event subsystem SHALL publish an event with a diagnostic summary
- **AND** the event SHALL be visible through existing structured logging
- **AND** the event SHALL NOT crash or stop the Host

### Requirement: Existing Log And UI Visibility

The existing structured log and Web log view SHALL show UDS request and response summaries through the current event pipeline.

#### Scenario: 文件日志包含 UDS 事件
- **GIVEN** structured file logging is configured
- **WHEN** UDS request, response, or error events are published
- **THEN** the file log SHALL contain those events
- **AND** the logged events SHALL preserve SID and NRC summaries when available

#### Scenario: Web 日志显示 UDS 事件
- **GIVEN** the Web console event stream or recent-events API is available
- **WHEN** UDS events are published
- **THEN** the existing Web log view SHALL be able to display UDS request and response summaries
- **AND** no new Web UI page SHALL be required
- **AND** the implementation SHALL NOT add a UDS management or editing UI for this change

### Requirement: Scope Boundaries

The implementation SHALL remain limited to UDS dispatching, NRC response modeling, DoIP diagnostic forwarding, and event visibility.

#### Scenario: 不实现具体 UDS 正响应服务
- **GIVEN** task-011 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement positive response behavior for DiagnosticSessionControl, TesterPresent, ReadDataByIdentifier, WriteDataByIdentifier, DTC services, RoutineControl, SecurityAccess, or flashing services
- **AND** it SHALL NOT add configured DID, DTC, Routine, SecurityAccess, or flashing service execution

#### Scenario: 不实现 ECU 状态机细节
- **GIVEN** task-011 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement session state transitions
- **AND** it SHALL NOT implement ECU security state transitions
- **AND** it SHALL NOT implement P2/P2* timers or TesterPresent timeout behavior

#### Scenario: 不实现 SecurityAccess
- **GIVEN** task-011 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement seed/key generation
- **AND** it SHALL NOT implement failed attempt counters
- **AND** it SHALL NOT implement unlock levels or security delay timers

#### Scenario: 不新增 Web 功能或异常注入
- **GIVEN** task-011 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add a new Web UI page
- **AND** it SHALL NOT add manual NRC, custom UDS response, or fault injection controls
- **AND** it SHALL NOT add `0x78 ResponsePending` scheduling behavior

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
