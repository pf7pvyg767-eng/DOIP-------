# runtime-event-stream Specification

## Purpose
TBD - created by archiving change task-007. Update Purpose after archive.
## Requirements
### Requirement: 内存环形事件缓冲

The observability subsystem SHALL keep a bounded in-memory ring buffer of recently published `RuntimeEvent` instances.

#### Scenario: 保存最近事件且不无限增长
- **GIVEN** the event buffer has a configured capacity of 1000 events
- **WHEN** more than 1000 `RuntimeEvent` instances are published
- **THEN** the buffer SHALL retain no more than 1000 events
- **AND** the buffer SHALL discard the oldest events first
- **AND** the buffer SHALL keep the newest events available for recent-event reads

#### Scenario: 读取最新 N 条事件
- **GIVEN** the event buffer contains more events than the requested `limit`
- **WHEN** the recent-event API requests the latest N events
- **THEN** the buffer SHALL return no more than N events
- **AND** the returned events SHALL be the newest events available in chronological order or another documented stable order

#### Scenario: 按分类读取最近事件
- **GIVEN** the event buffer contains events from multiple categories
- **WHEN** recent events are requested with category `doip`
- **THEN** the result SHALL include only events whose category is `doip`
- **AND** the result SHALL still respect the requested `limit`

### Requirement: 最近事件读取 API

The WebApi SHALL provide an internal recent-event endpoint that returns bounded in-memory `RuntimeEvent` data.

#### Scenario: 获取最近事件
- **GIVEN** the WebApi is running and the in-memory event buffer contains events
- **WHEN** a client sends `GET /api/events/recent?limit=200`
- **THEN** the response SHALL contain no more than 200 recent `RuntimeEvent` items
- **AND** the response SHALL NOT read historical log files
- **AND** the response SHALL NOT perform full-text log search

#### Scenario: 按分类获取最近事件
- **GIVEN** the WebApi is running and the event buffer contains `doip` and non-`doip` events
- **WHEN** a client sends `GET /api/events/recent?limit=200&category=doip`
- **THEN** the response SHALL contain only `doip` category events
- **AND** the response SHALL contain no more than 200 events

#### Scenario: 限制过大的 limit
- **GIVEN** the client requests a `limit` greater than the server-defined maximum
- **WHEN** `GET /api/events/recent` is handled
- **THEN** the WebApi SHALL clamp the response to the server-defined maximum
- **AND** the WebApi SHALL continue to return a successful bounded response when other parameters are valid

### Requirement: WebSocket 实时事件流

The WebApi SHALL provide an internal WebSocket event stream that pushes published `RuntimeEvent` JSON messages to connected clients.

#### Scenario: 客户端接收新事件
- **GIVEN** a client is connected to `WS /api/events/stream`
- **WHEN** a new `RuntimeEvent` is published
- **THEN** the WebApi SHALL send the event to the connected client as JSON
- **AND** the JSON payload SHALL preserve the event `id`, `timestamp`, `level`, `category`, `name`, `message`, `connectionId`, and `data` fields

#### Scenario: 多客户端接收事件
- **GIVEN** multiple clients are connected to `WS /api/events/stream`
- **WHEN** a new `RuntimeEvent` is published
- **THEN** each connected client SHALL be eligible to receive the event
- **AND** failure or disconnection of one client SHALL NOT prevent the service from continuing to serve other clients

#### Scenario: 断开后服务端保持稳定
- **GIVEN** a client is connected to `WS /api/events/stream`
- **WHEN** the client disconnects or the connection is cancelled
- **THEN** the WebApi SHALL release the client subscription
- **AND** the WebApi SHALL NOT crash because of the disconnect
- **AND** subsequent clients SHALL still be able to connect to the event stream

### Requirement: 控制台事件订阅

The WebConsole SHALL load recent events and subscribe to the realtime event stream for live updates.

#### Scenario: 页面打开后看到启动事件
- **GIVEN** the simulator has published a `runtime.started` event
- **WHEN** the user opens the WebConsole logs view
- **THEN** the WebConsole SHALL load recent events from the backend
- **AND** the logs view SHALL display the startup event when it is still available in the in-memory buffer

#### Scenario: 配置保存后实时出现配置事件
- **GIVEN** the WebConsole logs view is open and subscribed to the event stream
- **WHEN** simulator configuration is saved and a `config.saved` event is published
- **THEN** the logs view SHALL append the configuration event without requiring a full page refresh

#### Scenario: 前端断开后重连
- **GIVEN** the WebConsole event stream connection is disconnected
- **WHEN** the frontend reconnects using its configured reconnect behavior
- **THEN** the WebConsole SHALL continue to display the logs view
- **AND** the backend service SHALL NOT fail because of the reconnect
- **AND** the WebConsole SHALL be able to load recent events again

#### Scenario: 前端事件数量受限
- **GIVEN** the WebConsole event list capacity is configured as 1000 events
- **WHEN** the WebConsole receives more than 1000 events through recent loading and realtime streaming
- **THEN** the WebConsole SHALL retain no more than 1000 events in UI state
- **AND** the WebConsole SHALL discard the oldest UI events first

### Requirement: 控制台日志列表和过滤

The WebConsole SHALL provide a logs view that lists runtime events and supports level and category filtering.

#### Scenario: 展示日志列表
- **GIVEN** the WebConsole has loaded or received `RuntimeEvent` items
- **WHEN** the logs view renders
- **THEN** the logs view SHALL display each visible event's timestamp
- **AND** the logs view SHALL display each visible event's level
- **AND** the logs view SHALL display each visible event's category
- **AND** the logs view SHALL display each visible event's name
- **AND** the logs view SHALL display each visible event's message

#### Scenario: 按等级过滤
- **GIVEN** the logs view contains events with multiple levels
- **WHEN** the user selects a level filter such as `error`
- **THEN** the logs view SHALL display only events matching the selected level
- **AND** the logs view SHALL keep the underlying bounded event list available for later filter changes

#### Scenario: 按分类过滤
- **GIVEN** the logs view contains events from multiple categories
- **WHEN** the user selects a category filter such as `doip`
- **THEN** the logs view SHALL display only events matching the selected category
- **AND** the logs view SHALL keep the underlying bounded event list available for later filter changes

#### Scenario: 组合过滤
- **GIVEN** the logs view contains events with multiple levels and categories
- **WHEN** the user selects both a level filter and a category filter
- **THEN** the logs view SHALL display only events matching both selected filters

### Requirement: 范围限制

The realtime event stream and logs view implementation SHALL remain limited to internal realtime observation and bounded recent-event display.

#### Scenario: 不实现历史日志搜索
- **GIVEN** task-007 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add historical log search
- **AND** it SHALL NOT add file-log indexing
- **AND** it SHALL NOT add full-text query behavior

#### Scenario: 不实现复杂图表
- **GIVEN** task-007 is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT add complex charts
- **AND** it SHALL remain a logs list and filter view

#### Scenario: 不保证 100M 吞吐
- **GIVEN** task-007 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT claim or implement a 100M throughput guarantee
- **AND** it SHALL NOT introduce high-throughput optimization work beyond the bounded event stream needed for the console

#### Scenario: 不扩展 DoIP/UDS 协议行为
- **GIVEN** task-007 is implemented
- **WHEN** backend protocol modules are inspected
- **THEN** the change SHALL NOT add new DoIP runtime behavior
- **AND** the change SHALL NOT add new UDS runtime behavior

