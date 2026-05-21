# realtime-observability-ui Specification

## Purpose
TBD - created by archiving change task-014. Update Purpose after archive.
## Requirements
### Requirement: Connections Snapshot API

The system SHALL expose a read-only `GET /api/connections` endpoint that returns current connection snapshots.

#### Scenario: 返回当前连接列表
- **GIVEN** one or more clients are connected to the simulator
- **WHEN** a caller sends `GET /api/connections`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL contain a JSON array of connection snapshots
- **AND** each snapshot SHALL include connection ID, transport, remote endpoint, Routing Activation status, tester logical address when available, ECU logical address when available, connected timestamp, and current connection state

#### Scenario: 没有连接时返回空列表
- **GIVEN** no clients are currently connected
- **WHEN** a caller sends `GET /api/connections`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL be an empty JSON array

#### Scenario: 只读快照不改变连接状态
- **GIVEN** a client is connected
- **WHEN** `GET /api/connections` is called repeatedly
- **THEN** the calls SHALL NOT open, close, activate, or mutate any connection

### Requirement: ECU State Snapshot API

The system SHALL expose a read-only `GET /api/ecu/state` endpoint that returns the current ECU runtime state.

#### Scenario: 返回 ECU 当前状态
- **GIVEN** the simulator runtime has an ECU runtime state
- **WHEN** a caller sends `GET /api/ecu/state`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL include ECU logical address
- **AND** the response body SHALL include the current diagnostic session
- **AND** the response body SHALL include the security state summary
- **AND** the response body SHALL include the last TesterPresent timestamp when available

#### Scenario: 会话切换后快照反映新状态
- **GIVEN** the ECU current session has changed from default session to extended or programming session
- **WHEN** a caller sends `GET /api/ecu/state`
- **THEN** the returned current diagnostic session SHALL match the runtime state
- **AND** the API SHALL NOT infer state from frontend-only data

### Requirement: Runtime Event Reuse For Realtime UI

The realtime observation UI SHALL use the existing `RuntimeEvent` WebSocket stream for live updates.

#### Scenario: 订阅现有事件流
- **GIVEN** the WebConsole realtime observation view is mounted
- **WHEN** the view starts listening for live updates
- **THEN** it SHALL connect to the existing runtime event WebSocket endpoint
- **AND** it SHALL NOT create a second realtime protocol or endpoint for this task

#### Scenario: 处理连接事件
- **GIVEN** the UI receives a `RuntimeEvent` named `connection.opened`
- **WHEN** the event contains a connection ID and connection summary data
- **THEN** the UI SHALL add or update the corresponding connection row
- **AND** the row SHALL be shown as open or active

#### Scenario: 处理连接关闭事件
- **GIVEN** the UI has an existing connection row
- **WHEN** the UI receives a `RuntimeEvent` named `connection.closed` for the same connection ID
- **THEN** the UI SHALL mark that connection as closed
- **AND** the UI SHALL preserve enough row data to show that the connection closed

#### Scenario: 处理 DoIP 报文事件
- **GIVEN** the UI receives `doip.frame.received` or `doip.frame.sent`
- **WHEN** the event includes available frame summary data
- **THEN** the UI SHALL append a DoIP trace row
- **AND** the row SHALL identify direction, timestamp, connection ID when available, payload type or name when available, and a byte or message summary when available

#### Scenario: 处理 UDS 报文事件
- **GIVEN** the UI receives `uds.request.received` or `uds.response.sent`
- **WHEN** the event includes available UDS summary data
- **THEN** the UI SHALL append a UDS trace row
- **AND** the row SHALL identify request/response direction, timestamp, connection ID when available, service ID or response SID when available, and a byte or message summary when available

#### Scenario: 处理状态会话事件
- **GIVEN** the UI displays ECU state
- **WHEN** the UI receives a `RuntimeEvent` named `state.session.changed`
- **THEN** the ECU state panel SHALL update the displayed diagnostic session
- **AND** the update SHALL occur without requiring a full page reload

#### Scenario: 兼容现有会话事件命名
- **GIVEN** the implementation baseline still publishes `uds.session.changed`
- **WHEN** the UI receives that event with session state data
- **THEN** the UI MAY update the ECU state panel from that event
- **AND** this compatibility SHALL NOT replace the requirement to support `state.session.changed`

### Requirement: Connection List UI

The WebConsole SHALL provide a connection list UI for observing client connections.

#### Scenario: 客户端连接后显示连接
- **GIVEN** a client connects to the simulator
- **WHEN** the connection snapshot API or `connection.opened` event reports the connection
- **THEN** the UI SHALL display the connection in the connection list
- **AND** the row SHALL include connection ID, transport, remote endpoint, Routing Activation status, logical addresses when available, and connection state

#### Scenario: Routing Activation 后更新连接行
- **GIVEN** a connection is displayed before Routing Activation
- **WHEN** RuntimeEvent or snapshot data reports that Routing Activation completed
- **THEN** the UI SHALL update the same connection row
- **AND** the row SHALL show tester and ECU logical addresses when available

#### Scenario: 断开连接后显示关闭
- **GIVEN** a connection is displayed in the connection list
- **WHEN** the client disconnects and the UI receives `connection.closed`
- **THEN** the UI SHALL show that connection as closed
- **AND** the connection SHALL NOT appear as active

### Requirement: DoIP Message List UI

The WebConsole SHALL provide a DoIP message list UI for observing DoIP frame traffic.

#### Scenario: 显示 DoIP 请求和响应
- **GIVEN** a connected client sends DoIP frames and the simulator sends DoIP frames
- **WHEN** the UI receives `doip.frame.received` and `doip.frame.sent` events
- **THEN** the DoIP message list SHALL display received and sent rows
- **AND** each row SHALL show direction, timestamp, connection ID when available, payload type or frame name when available, and a readable payload summary

#### Scenario: 缺失摘要字段时降级显示
- **GIVEN** a DoIP RuntimeEvent lacks optional frame summary data
- **WHEN** the UI renders the DoIP row
- **THEN** the UI SHALL still render the row
- **AND** missing optional values SHALL be displayed as unavailable or empty values rather than breaking the view

### Requirement: UDS Message List UI

The WebConsole SHALL provide a UDS message list UI for observing UDS request and response traffic.

#### Scenario: 显示 UDS 请求和响应
- **GIVEN** a client sends a UDS request through DoIP and the simulator returns a UDS response
- **WHEN** the UI receives `uds.request.received` and `uds.response.sent` events
- **THEN** the UDS message list SHALL display both the request and the response
- **AND** the rows SHALL show direction, timestamp, connection ID when available, service ID or response SID when available, and a readable byte summary

#### Scenario: DID 请求响应可观察
- **GIVEN** DID `0xF190` is configured and a client sends UDS request `22 F1 90`
- **WHEN** the simulator returns the UDS positive response beginning with `62 F1 90`
- **THEN** the UDS message list SHALL show the request
- **AND** the UDS message list SHALL show the response

### Requirement: ECU State Panel

The WebConsole SHALL provide an ECU state panel that reflects the current ECU runtime state.

#### Scenario: 初次加载状态面板
- **GIVEN** the WebConsole realtime observation view loads
- **WHEN** `GET /api/ecu/state` returns a state snapshot
- **THEN** the ECU state panel SHALL display the ECU logical address
- **AND** it SHALL display the current diagnostic session
- **AND** it SHALL display the security state summary
- **AND** it SHALL display the last TesterPresent timestamp when available

#### Scenario: 会话切换后实时更新
- **GIVEN** the ECU state panel currently displays default session
- **WHEN** a client sends DiagnosticSessionControl and the UI receives a session changed event
- **THEN** the ECU state panel SHALL update to the new session
- **AND** the update SHALL be visible without page reload

### Requirement: Basic Filtering

The WebConsole SHALL provide basic filtering for connection and message observation lists.

#### Scenario: 按连接过滤报文
- **GIVEN** DoIP or UDS message rows exist for multiple connections
- **WHEN** the user selects or enters a connection filter
- **THEN** the message lists SHALL show rows matching that connection
- **AND** non-matching rows SHALL be hidden from the filtered view

#### Scenario: 按方向过滤报文
- **GIVEN** a message list contains received and sent rows
- **WHEN** the user applies a direction filter
- **THEN** the list SHALL show only rows matching the selected direction

#### Scenario: 按关键词过滤报文
- **GIVEN** a message list contains rows with service IDs, event names, payload type names, or byte summaries
- **WHEN** the user enters a keyword
- **THEN** the list SHALL show rows whose searchable fields contain that keyword case-insensitively

### Requirement: Scope Boundaries

The implementation SHALL remain limited to realtime observation UI, read-only state APIs, basic filtering, and RuntimeEvent consumption.

#### Scenario: 不实现报文重放
- **GIVEN** task-014 is implemented
- **WHEN** the UI and API surface are inspected
- **THEN** the change SHALL NOT add message replay commands
- **AND** it SHALL NOT resend captured DoIP or UDS payloads

#### Scenario: 不实现图表分析
- **GIVEN** task-014 is implemented
- **WHEN** the WebConsole is inspected
- **THEN** the change SHALL NOT add charts, trend analysis, aggregate analytics, or protocol statistics dashboards

#### Scenario: 不实现 pcap 下载
- **GIVEN** task-014 is implemented
- **WHEN** the API and UI surface are inspected
- **THEN** the change SHALL NOT add pcap download endpoints
- **AND** it SHALL NOT add pcap download controls

#### Scenario: 不新增诊断服务或管理界面
- **GIVEN** task-014 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT add new UDS diagnostic services
- **AND** it SHALL NOT add DID, DTC, Routine, Flash, or SecurityAccess editing workflows

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

