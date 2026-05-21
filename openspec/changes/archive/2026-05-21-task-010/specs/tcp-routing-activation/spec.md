# Spec: TCP 连接管理和路由激活

**Change ID:** `task-010`
**Status:** Draft

---

## ADDED Requirements

### Requirement: TCP DoIP Server Lifecycle

The Host runtime SHALL start a TCP DoIP server when the simulator starts with TCP DoIP enabled or configured.

#### Scenario: Host 启动 TCP DoIP 服务
- **GIVEN** the simulator Host starts with a valid `SimulatorConfig`
- **WHEN** runtime services are started
- **THEN** the system SHALL bind a TCP listener for DoIP
- **AND** the listener SHALL use the configured DoIP TCP port
- **AND** the listener SHALL accept TCP client connections

#### Scenario: Host 停止释放 TCP 资源
- **GIVEN** the TCP DoIP server is running
- **WHEN** the Host is stopped or cancellation is requested
- **THEN** the TCP listener SHALL stop accepting new clients
- **AND** active TCP connections SHALL be closed or cancelled
- **AND** sockets, streams, and connection tasks SHALL be released without crashing the Host

#### Scenario: TCP 监听失败报告错误
- **GIVEN** the configured DoIP TCP endpoint cannot be bound
- **WHEN** the Host starts the TCP DoIP server
- **THEN** startup SHALL fail or report a clear runtime error
- **AND** the error SHALL identify the TCP DoIP endpoint or port involved
- **AND** the system SHALL NOT silently run without TCP DoIP when TCP service is configured to start

### Requirement: TCP Connection Registry

The TCP implementation SHALL maintain connection state for each accepted TCP client.

#### Scenario: 连接建立时创建状态
- **GIVEN** the TCP DoIP server is listening
- **WHEN** a client establishes a TCP connection
- **THEN** the system SHALL create a connection record
- **AND** the record SHALL include a connection ID
- **AND** the record SHALL include the remote endpoint
- **AND** the record SHALL include the connected timestamp
- **AND** the record SHALL initially mark routing activation as not completed

#### Scenario: Routing Activation 成功后更新状态
- **GIVEN** a TCP connection record exists
- **WHEN** Routing Activation succeeds for the connection
- **THEN** the registry SHALL mark the connection as routing activated
- **AND** the registry SHALL store the tester logical source address
- **AND** the registry SHALL store the ECU logical address used for the activation response

#### Scenario: 连接断开后清理状态
- **GIVEN** a TCP connection record exists
- **WHEN** the client disconnects or the connection is closed by the Host
- **THEN** the registry SHALL remove the connection record or mark it disconnected
- **AND** later snapshots SHALL NOT present the connection as an active routed connection

#### Scenario: 连接超时后清理状态
- **GIVEN** a TCP connection is idle beyond the configured or default timeout
- **WHEN** the timeout is detected
- **THEN** the system SHALL publish a timeout event
- **AND** the registry SHALL remove the connection record or mark it disconnected
- **AND** the connection socket SHALL be closed or cancelled

### Requirement: TCP Stream Frame Assembly

The TCP implementation SHALL assemble DoIP frames from a byte stream before dispatching protocol handlers.

#### Scenario: 半包 frame 组包
- **GIVEN** a DoIP frame arrives across multiple TCP reads
- **WHEN** the stream reader receives only part of the frame
- **THEN** the stream reader SHALL buffer the partial bytes
- **AND** it SHALL NOT dispatch an incomplete frame
- **AND** it SHALL dispatch exactly one complete frame after all bytes arrive

#### Scenario: 粘包 frame 拆分
- **GIVEN** multiple DoIP frames arrive in one TCP read
- **WHEN** the stream reader processes the read buffer
- **THEN** it SHALL produce each complete DoIP frame separately
- **AND** it SHALL preserve frame order
- **AND** it SHALL NOT merge payloads from different frames

#### Scenario: 连续半包和粘包混合处理
- **GIVEN** TCP reads contain a mix of partial frames and multiple complete frames
- **WHEN** the stream reader processes the bytes over time
- **THEN** it SHALL dispatch every complete frame once
- **AND** it SHALL retain only bytes belonging to an incomplete trailing frame

#### Scenario: 复用 DoIP codec
- **GIVEN** the stream reader extracts a complete DoIP frame candidate
- **WHEN** the candidate is validated or decoded
- **THEN** the implementation SHALL use the existing DoIP frame codec
- **AND** it SHALL NOT duplicate DoIP fixed-header validation outside the codec beyond the minimal length needed for stream framing
- **AND** codec validation failures SHALL be reported as `doip` protocol error events without crashing the TCP server

### Requirement: Routing Activation Handling

The TCP DoIP protocol handler SHALL process Routing Activation Request frames and return Routing Activation Response frames.

#### Scenario: 合法源地址 Routing Activation 成功
- **GIVEN** a TCP client is connected
- **AND** the client sends a valid Routing Activation Request
- **AND** the tester logical source address is allowed by the configured source address whitelist
- **WHEN** the request is handled
- **THEN** the system SHALL send a Routing Activation Response on the same TCP connection
- **AND** the response SHALL indicate activation success
- **AND** the connection SHALL be marked routing activated
- **AND** the tester logical source address SHALL be recorded in the connection state

#### Scenario: 非白名单源地址 Routing Activation 失败
- **GIVEN** a TCP client is connected
- **AND** the client sends a valid Routing Activation Request
- **AND** the tester logical source address is not allowed by the configured source address whitelist
- **WHEN** the request is handled
- **THEN** the system SHALL send a Routing Activation Response on the same TCP connection
- **AND** the response SHALL indicate activation failure or denied source address
- **AND** the connection SHALL NOT be marked routing activated
- **AND** the failure SHALL be published as a `doip` event

#### Scenario: Routing Activation 不触发 UDS 业务
- **GIVEN** Routing Activation succeeds
- **WHEN** the connection becomes routing activated
- **THEN** the system SHALL NOT process UDS service requests as part of this change
- **AND** the system SHALL NOT generate UDS business responses
- **AND** the system SHALL NOT start diagnostic message forwarding beyond maintaining routing activation state

### Requirement: Source Address Whitelist

Routing Activation SHALL enforce the configured tester logical source address whitelist.

#### Scenario: 白名单为空或未配置时使用明确默认策略
- **GIVEN** the source address whitelist is empty or not configured
- **WHEN** a Routing Activation Request is received
- **THEN** the system SHALL apply a deterministic default policy
- **AND** the policy SHALL be covered by tests or documented implementation notes
- **AND** the system SHALL NOT silently allow arbitrary tester source addresses unless that is the documented default

#### Scenario: 白名单按 tester logical source address 校验
- **GIVEN** a source address whitelist is configured
- **WHEN** a Routing Activation Request is received
- **THEN** the system SHALL compare the request tester logical source address with the whitelist
- **AND** it SHALL NOT treat the remote IP address as a substitute for the tester logical source address
- **AND** the response behavior SHALL depend on this logical address check

### Requirement: Alive Check Basic Support

The TCP DoIP protocol handler SHALL provide basic Alive Check support.

#### Scenario: 响应 Alive Check Request
- **GIVEN** a TCP client is connected
- **WHEN** the client sends a valid Alive Check Request frame
- **THEN** the system SHALL send an Alive Check Response on the same TCP connection
- **AND** the response SHALL be encoded as a DoIP frame
- **AND** the TCP server SHALL keep processing later frames on the connection

#### Scenario: Alive Check 事件可见
- **GIVEN** the system receives or sends Alive Check frames
- **WHEN** Alive Check handling occurs
- **THEN** the runtime event subsystem SHALL publish `doip` category events
- **AND** the events SHALL include the connection ID or remote endpoint
- **AND** the events SHALL summarize request and response handling without requiring raw packet dumps

#### Scenario: 不实现复杂异常注入
- **GIVEN** Alive Check support is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add configurable packet loss, delayed response injection, malformed response injection, or other complex fault injection behavior

### Requirement: TCP DoIP Runtime Events

The TCP implementation SHALL publish structured DoIP events for connection lifecycle, Routing Activation, Alive Check, and protocol errors.

#### Scenario: 连接创建事件
- **GIVEN** a TCP client connects
- **WHEN** the connection is accepted
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify TCP connection creation
- **AND** the event data SHALL include the connection ID and remote endpoint

#### Scenario: 连接断开事件
- **GIVEN** an active TCP connection exists
- **WHEN** the client disconnects or the Host closes the connection
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify TCP connection disconnection
- **AND** the event data SHALL include the connection ID or remote endpoint

#### Scenario: 连接超时事件
- **GIVEN** an active TCP connection exceeds the configured or default timeout
- **WHEN** the timeout is detected
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify TCP connection timeout
- **AND** the event data SHALL include the connection ID or remote endpoint

#### Scenario: Routing Activation 事件
- **GIVEN** a Routing Activation Request is processed
- **WHEN** the handler sends a success or failure response
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event data SHALL include the tester logical source address
- **AND** the event data SHALL include whether activation succeeded
- **AND** failed whitelist checks SHALL be distinguishable from successful activation

#### Scenario: 协议错误事件
- **GIVEN** a TCP frame fails DoIP codec validation or routing activation payload validation
- **WHEN** the handler rejects the frame
- **THEN** the runtime event subsystem SHALL publish a warning or error event with category `doip`
- **AND** the event data SHALL include the connection ID or remote endpoint
- **AND** the TCP server SHALL continue processing later frames when the connection remains valid

### Requirement: Existing Log And UI Visibility

The existing structured log and Web log view SHALL show TCP DoIP connection and routing activation summaries through the current event pipeline.

#### Scenario: 文件日志包含 TCP DoIP 事件
- **GIVEN** structured file logging is configured
- **WHEN** TCP connection, routing activation, alive check, or timeout events are published
- **THEN** the file log SHALL contain those `doip` events
- **AND** the logged events SHALL preserve their structured event data

#### Scenario: Web 日志显示 TCP DoIP 事件
- **GIVEN** the Web console event stream or recent-events API is available
- **WHEN** TCP DoIP events are published
- **THEN** the existing Web log view SHALL be able to display connection, routing activation, alive check, and timeout summaries
- **AND** no new Web UI page SHALL be required
- **AND** filtering by category `doip` SHALL include the TCP DoIP events

### Requirement: Local TCP Verification

The implementation SHALL be testable with a local TCP client.

#### Scenario: 本地 TCP client 完成 Routing Activation
- **GIVEN** the Host or TCP server is running on a test TCP endpoint
- **WHEN** a local TCP client connects and sends a valid Routing Activation Request with an allowed tester logical source address
- **THEN** the client SHALL receive a Routing Activation Response
- **AND** the response SHALL decode successfully with the DoIP codec
- **AND** the response SHALL indicate activation success

#### Scenario: 本地 TCP client 验证白名单失败
- **GIVEN** the Host or TCP server is running with a source address whitelist
- **WHEN** a local TCP client connects and sends a Routing Activation Request with a non-whitelisted tester logical source address
- **THEN** the client SHALL receive a Routing Activation Response
- **AND** the response SHALL indicate activation failure or denied source address
- **AND** the connection SHALL NOT be presented as routing activated

#### Scenario: 测试不依赖固定系统端口
- **GIVEN** automated tests verify TCP DoIP behavior
- **WHEN** the tests start the TCP server
- **THEN** the tests SHALL use isolated configuration or a test-controlled endpoint
- **AND** the tests SHALL NOT require the production DoIP TCP port to be free on the machine
- **AND** the tests SHALL clean up TCP resources before completion

### Requirement: Scope Boundaries

The TCP connection management implementation SHALL remain limited to TCP DoIP connection setup, routing activation, source address validation, frame assembly, events, and basic Alive Check.

#### Scenario: 不处理 UDS 业务响应
- **GIVEN** task-010 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement UDS service handlers
- **AND** it SHALL NOT generate UDS business responses
- **AND** it SHALL NOT implement diagnostic message forwarding

#### Scenario: 不实现 TLS
- **GIVEN** task-010 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement TLS listeners
- **AND** it SHALL NOT add TLS handshake, certificate, or encryption behavior

#### Scenario: 不实现复杂异常注入
- **GIVEN** task-010 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add complex fault injection controls
- **AND** it SHALL NOT add malformed-frame injection tooling
- **AND** it SHALL NOT add configurable network impairment simulation

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

