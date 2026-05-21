# runtime-events-logging Specification

## Purpose
TBD - created by archiving change task-006. Update Purpose after archive.
## Requirements
### Requirement: 结构化运行事件模型

The Core runtime event contract SHALL define a `RuntimeEvent` model that can represent simulator runtime, configuration, protocol, state, fault, TLS, and PCAP-category events without implementing PCAP behavior.

#### Scenario: 定义 RuntimeEvent 核心字段
- **GIVEN** 开发者需要发布运行时事件
- **WHEN** `RuntimeEvent` is defined
- **THEN** the event model SHALL include `id`
- **AND** the event model SHALL include `timestamp`
- **AND** the event model SHALL include `level`
- **AND** the event model SHALL include `category`
- **AND** the event model SHALL include `name`
- **AND** the event model SHALL include `message`
- **AND** the event model SHALL include nullable `connectionId`
- **AND** the event model SHALL include structured `data`

#### Scenario: 支持事件分类
- **GIVEN** an event is created
- **WHEN** the event category is assigned
- **THEN** the category SHALL support `system`
- **AND** the category SHALL support `config`
- **AND** the category SHALL support `connection`
- **AND** the category SHALL support `doip`
- **AND** the category SHALL support `uds`
- **AND** the category SHALL support `state`
- **AND** the category SHALL support `fault`
- **AND** the category SHALL support `tls`
- **AND** the category SHALL support `pcap` as a classification value only

#### Scenario: 支持事件等级
- **GIVEN** an event is created
- **WHEN** the event level is assigned
- **THEN** the level SHALL support informational events
- **AND** the level SHALL support warning events
- **AND** the level SHALL support error events

### Requirement: 事件发布接口

The runtime event subsystem SHALL provide an event publishing interface that modules can use to publish `RuntimeEvent` instances without directly depending on file IO.

#### Scenario: 发布运行事件
- **GIVEN** a module has a `RuntimeEvent`
- **WHEN** the module publishes the event through the event publishing interface
- **THEN** the event subsystem SHALL dispatch the event to configured event sinks
- **AND** the publishing module SHALL NOT need to know the file logging implementation

#### Scenario: 未配置 sink 时保持运行
- **GIVEN** no file event sink is configured
- **WHEN** a module publishes a `RuntimeEvent`
- **THEN** the event publishing interface SHALL accept the event
- **AND** the main process SHALL continue running
- **AND** no Web realtime push or log query behavior SHALL be required

### Requirement: 异步 UTF-8 文件日志写入

The observability logging subsystem SHALL write published `RuntimeEvent` instances to a UTF-8 log file asynchronously.

#### Scenario: 写入单条事件
- **GIVEN** a file event sink is configured with a writable log file path
- **WHEN** a `RuntimeEvent` is published
- **THEN** the sink SHALL write the event to the log file
- **AND** the log entry SHALL preserve the event `id`, `timestamp`, `level`, `category`, `name`, `message`, `connectionId`, and `data` fields
- **AND** the log file SHALL be encoded as UTF-8

#### Scenario: 写入多条事件
- **GIVEN** a file event sink is configured with a writable log file path
- **WHEN** multiple `RuntimeEvent` instances are published
- **THEN** the sink SHALL write all published events to the log file
- **AND** each event SHALL remain independently identifiable in the log file

#### Scenario: 日志写入失败时降级
- **GIVEN** the configured log file path cannot be written
- **WHEN** a `RuntimeEvent` is published
- **THEN** the write failure SHALL be captured as a degraded logging error
- **AND** the main process SHALL NOT crash because of the logging failure
- **AND** the event publishing call SHALL NOT require the caller to implement recovery logic

### Requirement: 启动和停止事件

The host runtime SHALL publish lifecycle events for simulator startup and shutdown.

#### Scenario: 启动事件写入日志
- **GIVEN** file event logging is configured
- **WHEN** the simulator host starts successfully
- **THEN** the runtime SHALL publish a `runtime.started` event
- **AND** the event category SHALL be `system`
- **AND** the event level SHALL be informational
- **AND** the event SHALL be written to the log file

#### Scenario: 停止事件写入日志
- **GIVEN** file event logging is configured and the simulator host is running
- **WHEN** the simulator host stops
- **THEN** the runtime SHALL publish a `runtime.stopped` event
- **AND** the event category SHALL be `system`
- **AND** the event level SHALL be informational
- **AND** the event SHALL be written to the log file

### Requirement: 配置加载和保存事件

The configuration subsystem SHALL publish file-loggable events when configuration is loaded or saved.

#### Scenario: 配置加载事件写入日志
- **GIVEN** file event logging is configured
- **WHEN** simulator configuration is loaded successfully
- **THEN** the configuration subsystem SHALL publish a `config.loaded` event
- **AND** the event category SHALL be `config`
- **AND** the event level SHALL be informational
- **AND** the event SHALL be written to the log file

#### Scenario: 配置保存事件写入日志
- **GIVEN** file event logging is configured
- **WHEN** simulator configuration is saved successfully
- **THEN** the configuration subsystem SHALL publish a `config.saved` event
- **AND** the event category SHALL be `config`
- **AND** the event level SHALL be informational
- **AND** the event SHALL be written to the log file

### Requirement: 范围限制

The runtime event and file logging implementation SHALL remain limited to structured event publishing and asynchronous file persistence.

#### Scenario: 不实现 Web 实时推送
- **GIVEN** task-006 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add WebSocket, Server-Sent Events, polling endpoints, or other Web realtime event push behavior

#### Scenario: 不实现日志查询
- **GIVEN** task-006 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add log query APIs
- **AND** it SHALL NOT add log query UI
- **AND** it SHALL NOT add log indexing or search behavior

#### Scenario: 不实现高吞吐优化
- **GIVEN** task-006 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT introduce high-throughput batching, queue tuning, backpressure protocols, or performance optimization work beyond the minimal asynchronous file write required for this task

#### Scenario: 不实现 PCAP
- **GIVEN** task-006 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add PCAP capture
- **AND** it SHALL NOT add PCAP parsing
- **AND** it SHALL NOT add PCAP file writing
- **AND** it SHALL NOT add PCAP query or display behavior

