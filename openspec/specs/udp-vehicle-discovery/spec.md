# udp-vehicle-discovery Specification

## Purpose
TBD - created by archiving change task-009. Update Purpose after archive.
## Requirements
### Requirement: UDP DoIP Server Lifecycle

The Host runtime SHALL start a UDP DoIP server for vehicle discovery when the simulator starts.

#### Scenario: Host 启动 UDP discovery 服务
- **GIVEN** the simulator Host starts with a valid `SimulatorConfig`
- **WHEN** runtime services are started
- **THEN** the system SHALL bind a UDP listener for DoIP discovery
- **AND** the listener SHALL use the configured DoIP UDP port
- **AND** the listener SHALL be able to receive DoIP UDP datagrams

#### Scenario: Host 停止释放 UDP 端口
- **GIVEN** the UDP DoIP server is running
- **WHEN** the Host is stopped or cancellation is requested
- **THEN** the UDP listener SHALL stop accepting datagrams
- **AND** the UDP socket SHALL be released
- **AND** background announcement or receive loops SHALL be cancelled without crashing the Host

#### Scenario: UDP 服务启动失败报告错误
- **GIVEN** the configured DoIP UDP endpoint cannot be bound
- **WHEN** the Host starts the UDP DoIP server
- **THEN** startup SHALL fail or report a clear runtime error
- **AND** the error SHALL identify the UDP DoIP endpoint or port involved
- **AND** the system SHALL NOT silently run without UDP discovery when discovery is configured to start

### Requirement: UDP Datagram Handling Contract

The UDP discovery implementation SHALL separate datagram transport from DoIP vehicle identification handling.

#### Scenario: 处理入站 datagram
- **GIVEN** the UDP transport receives a datagram from a remote endpoint
- **WHEN** the datagram is passed to the DoIP UDP handler
- **THEN** the handler SHALL receive the payload bytes
- **AND** the handler SHALL receive the remote endpoint
- **AND** the handler SHALL return zero or more outbound datagrams
- **AND** the transport SHALL send each outbound datagram to its target endpoint

#### Scenario: 复用 DoIP codec
- **GIVEN** an inbound datagram contains a DoIP frame
- **WHEN** the UDP handler parses the datagram
- **THEN** it SHALL use the existing DoIP frame codec
- **AND** it SHALL NOT duplicate fixed-header parsing logic outside the codec
- **AND** codec validation failures SHALL be logged as `doip` events without crashing the UDP server

### Requirement: Vehicle Identification Request Response

The UDP discovery handler SHALL respond to DoIP Vehicle Identification Request messages with a vehicle identification response.

#### Scenario: 响应基础车辆识别请求
- **GIVEN** the UDP DoIP server is running with a valid `SimulatorConfig`
- **AND** a client sends a valid Vehicle Identification Request datagram
- **WHEN** the handler processes the request
- **THEN** the system SHALL send a Vehicle Identification Response datagram to the requesting endpoint
- **AND** the response SHALL be encoded as a DoIP frame
- **AND** the response payload type SHALL identify a vehicle announcement or vehicle identification response according to the DoIP discovery payload contract used by the implementation

#### Scenario: 响应字段来自配置
- **GIVEN** `SimulatorConfig.entity` contains VIN, EID, GID, and logical address values
- **WHEN** the handler builds a Vehicle Identification Response or Vehicle Announcement payload
- **THEN** the payload SHALL include the VIN from `SimulatorConfig.entity`
- **AND** the payload SHALL include the EID from `SimulatorConfig.entity`
- **AND** the payload SHALL include the GID from `SimulatorConfig.entity`
- **AND** the payload SHALL include the logical address from `SimulatorConfig.entity`
- **AND** the implementation SHALL NOT hard-code these identity values in the handler or transport

#### Scenario: 支持 EID 定向请求
- **GIVEN** a client sends a valid Vehicle Identification Request with EID
- **WHEN** the EID matches `SimulatorConfig.entity.eid`
- **THEN** the system SHALL send a Vehicle Identification Response to the requesting endpoint
- **AND** the response identity fields SHALL come from `SimulatorConfig.entity`

#### Scenario: 支持 VIN 定向请求
- **GIVEN** a client sends a valid Vehicle Identification Request with VIN
- **WHEN** the VIN matches `SimulatorConfig.entity.vin`
- **THEN** the system SHALL send a Vehicle Identification Response to the requesting endpoint
- **AND** the response identity fields SHALL come from `SimulatorConfig.entity`

#### Scenario: 定向请求不匹配
- **GIVEN** a client sends a valid Vehicle Identification Request with EID or VIN
- **WHEN** the requested EID or VIN does not match `SimulatorConfig.entity`
- **THEN** the handler SHALL NOT send a false positive vehicle identification response
- **AND** the handler SHALL NOT start routing activation
- **AND** the handler SHALL NOT start any TCP workflow

### Requirement: Vehicle Announcement Sending

The simulator SHALL send Vehicle Announcement messages according to configuration.

#### Scenario: 按配置发送公告
- **GIVEN** Vehicle Announcement sending is enabled or configured
- **WHEN** the UDP discovery service starts or an announcement interval elapses
- **THEN** the system SHALL send a Vehicle Announcement datagram according to the configured announcement behavior
- **AND** the announcement SHALL be encoded as a DoIP frame
- **AND** the announcement identity fields SHALL come from `SimulatorConfig.entity`

#### Scenario: 公告发送可取消
- **GIVEN** Vehicle Announcement sending is active
- **WHEN** the Host is stopped or cancellation is requested
- **THEN** announcement sending SHALL stop
- **AND** no additional announcement datagrams SHALL be sent after shutdown completes
- **AND** cancellation SHALL NOT crash the Host

#### Scenario: 默认配置保持可运行
- **GIVEN** the simulator uses the default `SimulatorConfig`
- **WHEN** UDP discovery starts
- **THEN** Vehicle Identification Response generation SHALL have valid identity values
- **AND** Vehicle Announcement behavior SHALL have a deterministic default or be clearly disabled by default
- **AND** the default behavior SHALL be covered by tests or documented implementation notes

### Requirement: DoIP UDP Runtime Events

The UDP discovery implementation SHALL publish structured DoIP events for requests, responses, announcements, and protocol errors.

#### Scenario: 记录车辆识别请求事件
- **GIVEN** the UDP server receives a Vehicle Identification Request
- **WHEN** the request is accepted for handling
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify a vehicle identification request
- **AND** the event data SHALL include the remote endpoint
- **AND** the event data SHALL include a request summary without requiring raw packet dumps

#### Scenario: 记录车辆识别响应事件
- **GIVEN** the UDP handler sends a Vehicle Identification Response
- **WHEN** the response datagram is produced or sent
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify a vehicle identification response
- **AND** the event data SHALL include VIN
- **AND** the event data SHALL include logical address
- **AND** the event data SHALL include the remote endpoint

#### Scenario: 记录公告发送事件
- **GIVEN** the UDP discovery service sends a Vehicle Announcement
- **WHEN** the announcement datagram is produced or sent
- **THEN** the runtime event subsystem SHALL publish a `doip` category event
- **AND** the event name SHALL identify a vehicle announcement
- **AND** the event data SHALL include VIN
- **AND** the event data SHALL include logical address

#### Scenario: 记录协议错误事件
- **GIVEN** an inbound UDP datagram fails DoIP codec validation or vehicle identification payload validation
- **WHEN** the handler rejects the datagram
- **THEN** the runtime event subsystem SHALL publish a warning or error event with category `doip`
- **AND** the event data SHALL include the remote endpoint
- **AND** the event data SHALL include a machine-readable error code or equivalent summary
- **AND** the UDP server SHALL continue processing later datagrams

### Requirement: Existing Log And UI Visibility

The Web log view SHALL show UDP discovery request and response summaries through the existing event pipeline.

#### Scenario: 文件日志包含 discovery 事件
- **GIVEN** structured file logging is configured
- **WHEN** a Vehicle Identification Request is received and a response is sent
- **THEN** the file log SHALL contain `doip` events for the request and response
- **AND** the logged events SHALL preserve their structured event data

#### Scenario: Web 日志显示 discovery 事件
- **GIVEN** the Web console event stream or recent-events API is available
- **WHEN** UDP discovery events are published
- **THEN** the existing Web log view SHALL be able to display request and response summaries
- **AND** no new Web UI page SHALL be required
- **AND** filtering by category `doip` SHALL include the UDP discovery events

### Requirement: Local UDP Discovery Verification

The implementation SHALL be testable with a local UDP client.

#### Scenario: 本地 UDP client 发现模拟 ECU
- **GIVEN** the Host or UDP server is running on a test UDP endpoint
- **WHEN** a local UDP client sends a Vehicle Identification Request datagram
- **THEN** the client SHALL receive a Vehicle Identification Response datagram
- **AND** the response SHALL decode successfully with the DoIP codec
- **AND** the response identity fields SHALL match the test `SimulatorConfig.entity`

#### Scenario: 测试不依赖固定系统端口
- **GIVEN** automated tests verify UDP discovery
- **WHEN** the tests start the UDP server
- **THEN** the tests SHALL use isolated configuration or a test-controlled endpoint
- **AND** the tests SHALL NOT require the production DoIP port to be free on the machine
- **AND** the tests SHALL clean up UDP resources before completion

### Requirement: Scope Boundaries

The UDP vehicle discovery implementation SHALL remain limited to UDP discovery and announcement behavior.

#### Scenario: 不实现 TCP
- **GIVEN** task-009 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add TCP DoIP listening behavior
- **AND** it SHALL NOT add TCP connection management

#### Scenario: 不实现 routing activation
- **GIVEN** task-009 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement routing activation request handling
- **AND** it SHALL NOT create diagnostic routing sessions

#### Scenario: 不实现 TLS
- **GIVEN** task-009 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement TLS listeners
- **AND** it SHALL NOT add TLS handshake or certificate behavior

#### Scenario: 不扩展其他协议能力
- **GIVEN** task-009 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement UDS services
- **AND** it SHALL NOT implement diagnostic message forwarding
- **AND** it SHALL NOT implement PCAP capture

