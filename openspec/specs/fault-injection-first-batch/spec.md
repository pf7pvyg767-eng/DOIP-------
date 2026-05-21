# fault-injection-first-batch Specification

## Purpose
TBD - created by archiving change task-024. Update Purpose after archive.
## Requirements
### Requirement: Fault Profile Configuration Model

The system SHALL provide a fault profile configuration model for the first batch of reproducible fault injection scenarios.

#### Scenario: 加载 fault profile 字段
- **GIVEN** a JSON simulator configuration or WebApi request contains a fault profile object
- **WHEN** the profile is loaded
- **THEN** the system SHALL load `enabled`
- **AND** it SHALL load `responseDelayMs`
- **AND** it SHALL load `pauseResponses`
- **AND** it SHALL load `routingActivationFailure`
- **AND** it SHALL load `corruptNextDoipHeader.inverseVersion`
- **AND** it SHALL load `corruptNextDoipHeader.payloadLengthDelta`
- **AND** it SHALL support fields for next manual NRC and custom UDS response overrides.

#### Scenario: 默认禁用 fault profile
- **GIVEN** no fault profile is configured
- **WHEN** the simulator creates or loads default configuration
- **THEN** the system SHALL provide a default fault profile
- **AND** `enabled` SHALL be `false`
- **AND** response delay, pause, Routing Activation failure, DoIP header corruption, manual NRC, and custom UDS response overrides SHALL be inactive.

#### Scenario: 校验 fault profile
- **GIVEN** a caller submits a fault profile
- **WHEN** the simulator validates the profile
- **THEN** the system SHALL reject negative `responseDelayMs`
- **AND** it SHALL reject invalid NRC values
- **AND** it SHALL reject invalid UDS service identifiers
- **AND** it SHALL reject malformed custom UDS response bytes
- **AND** validation errors SHALL identify the fault profile field involved.

### Requirement: Fault Runtime State

The system SHALL maintain runtime fault state separately from static configuration so that one-shot faults can be consumed deterministically.

#### Scenario: 保存当前 fault 策略
- **GIVEN** the WebApi updates the fault profile
- **WHEN** the update is accepted
- **THEN** the runtime state SHALL store the active fault profile
- **AND** later DoIP and UDS processing SHALL use that current state
- **AND** the system SHALL publish or log a fault profile updated event.

#### Scenario: 一次性 DoIP header fault 被消费
- **GIVEN** `corruptNextDoipHeader` is configured for the next DoIP response
- **WHEN** the next eligible DoIP response is sent
- **THEN** the system SHALL apply the configured header corruption to that response
- **AND** it SHALL mark the one-shot DoIP header fault as consumed
- **AND** later DoIP responses SHALL NOT be corrupted by that consumed one-shot fault.

#### Scenario: 一次性 UDS override 被消费
- **GIVEN** a next manual NRC or custom UDS response override is configured for service `S`
- **WHEN** the next UDS request for service `S` is processed
- **THEN** the system SHALL apply the configured override
- **AND** it SHALL mark the override as consumed
- **AND** later requests for service `S` SHALL follow normal behavior unless another override is configured.

### Requirement: Fault Web API

The WebApi SHALL expose endpoints for querying and updating fault state and for triggering manual fault actions.

#### Scenario: 查询 fault 状态
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `GET /api/faults`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL include the current fault profile
- **AND** it SHALL include a runtime state summary for pause and one-shot fault settings
- **AND** the call SHALL NOT mutate fault state.

#### Scenario: 更新 fault profile
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `PUT /api/faults` with a valid profile
- **THEN** the API SHALL update the active fault profile
- **AND** the response SHALL include the updated profile or state summary
- **AND** the system SHALL publish or log a fault profile updated event.

#### Scenario: 拒绝非法 fault profile
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `PUT /api/faults` with invalid delay, NRC, service ID, payload length delta, or custom response bytes
- **THEN** the API SHALL reject the request with a clear validation error
- **AND** the active fault profile SHALL remain unchanged.

#### Scenario: 配置下一次 NRC
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `POST /api/faults/actions/next-nrc` for service `S` and NRC `N`
- **THEN** the system SHALL configure the next manual NRC override for service `S`
- **AND** the response SHALL expose the configured one-shot override
- **AND** invalid service or NRC values SHALL return a clear validation error.

#### Scenario: 手动断开目标连接
- **GIVEN** the WebApi is running
- **AND** a target TCP connection exists
- **WHEN** a caller sends `POST /api/faults/actions/disconnect` for that connection
- **THEN** the system SHALL close the target connection
- **AND** the connection registry SHALL mark the connection closed or remove it from active connections
- **AND** the system SHALL publish or log a fault disconnect event.

### Requirement: Fault Web Console Controls

The WebConsole SHALL provide controls for manually switching the first batch of fault strategies.

#### Scenario: 显示当前 fault profile
- **GIVEN** the WebConsole fault injection view is opened
- **WHEN** `GET /api/faults` returns the current state
- **THEN** the UI SHALL display whether fault injection is enabled
- **AND** it SHALL display response delay, pause state, Routing Activation failure state, DoIP header corruption settings, and next UDS override state.

#### Scenario: 切换异常策略
- **GIVEN** the WebConsole fault controls are available
- **WHEN** a user changes enablement, response delay, pause/resume, Routing Activation failure, or DoIP header corruption settings
- **THEN** the UI SHALL call the fault WebApi
- **AND** it SHALL refresh or update the displayed state from the API response
- **AND** it SHALL show validation errors when the API rejects the update.

#### Scenario: 触发手动动作
- **GIVEN** the WebConsole fault controls are available
- **WHEN** a user triggers manual disconnect or next NRC override
- **THEN** the UI SHALL call the corresponding WebApi action
- **AND** it SHALL reflect success or failure without requiring a page reload
- **AND** it SHALL NOT add long-running fault scripts or probabilistic orchestration controls.

### Requirement: Response Delay Fault

The system SHALL delay responses when response delay fault injection is enabled.

#### Scenario: 客户端感知响应延迟
- **GIVEN** fault injection is enabled
- **AND** `responseDelayMs` is greater than zero
- **AND** a client sends a request that would normally receive a DoIP or UDS response
- **WHEN** the simulator sends the response
- **THEN** the response SHALL be delayed by approximately the configured duration
- **AND** the client SHALL observe the delayed response
- **AND** the system SHALL publish or log that response delay was applied.

#### Scenario: 延迟禁用时不增加等待
- **GIVEN** fault injection is disabled or `responseDelayMs` is zero
- **WHEN** a client sends a request on the normal path
- **THEN** the simulator SHALL NOT add fault-profile response delay
- **AND** existing protocol timing behavior SHALL remain governed by prior requirements.

### Requirement: Pause And Resume Responses Fault

The system SHALL support pausing and resuming simulator responses without closing the client connection.

#### Scenario: 暂停响应导致客户端超时
- **GIVEN** fault injection is enabled
- **AND** `pauseResponses` is `true`
- **AND** a client sends a request that would normally receive a response
- **WHEN** the simulator processes the request
- **THEN** the simulator SHALL NOT send the response while pause remains active
- **AND** the TCP connection SHALL remain open unless another fault or client timeout closes it
- **AND** the client SHALL be able to observe a request timeout according to its own timeout setting.

#### Scenario: 恢复响应后后续请求可响应
- **GIVEN** responses are paused
- **WHEN** `pauseResponses` is changed to `false`
- **AND** the client sends a later request
- **THEN** the simulator SHALL process later requests according to the current fault profile
- **AND** it SHALL be able to send responses again when no other active fault prevents response output.

### Requirement: Manual TCP Disconnect Fault

The system SHALL support manually closing a target TCP connection through fault injection controls.

#### Scenario: 手动断开关闭目标连接
- **GIVEN** a TCP DoIP client connection is active
- **WHEN** a caller triggers manual disconnect for that connection
- **THEN** the simulator SHALL close the target TCP connection
- **AND** the client SHALL observe the connection close
- **AND** the connection registry and runtime events SHALL reflect the closed connection.

#### Scenario: 目标连接不存在
- **GIVEN** no active connection matches the requested disconnect target
- **WHEN** a caller triggers manual disconnect
- **THEN** the API SHALL return a clear not-found or conflict result according to existing WebApi conventions
- **AND** no unrelated connection SHALL be closed.

### Requirement: Routing Activation Failure Fault

The system SHALL support reproducible Routing Activation failure when configured.

#### Scenario: Routing Activation 失败可复现
- **GIVEN** fault injection is enabled
- **AND** `routingActivationFailure` is `true`
- **AND** a client sends a valid Routing Activation Request
- **WHEN** the DoIP handler processes the request
- **THEN** the simulator SHALL send a Routing Activation Response indicating failure
- **AND** the connection SHALL NOT be marked routing activated
- **AND** repeated valid Routing Activation Requests SHALL fail while the setting remains enabled.

#### Scenario: Routing Activation failure 禁用时恢复正常策略
- **GIVEN** `routingActivationFailure` is changed from `true` to `false`
- **WHEN** a client sends a later valid Routing Activation Request
- **THEN** Routing Activation SHALL follow the existing source address whitelist and normal activation rules
- **AND** the prior fault setting SHALL NOT continue to force failure.

### Requirement: Corrupt DoIP Header Faults

The system SHALL support one-shot corruption of selected DoIP response header fields.

#### Scenario: 错误 inverse version
- **GIVEN** fault injection is enabled
- **AND** `corruptNextDoipHeader.inverseVersion` is `true`
- **WHEN** the next eligible DoIP response is encoded for sending
- **THEN** the response SHALL contain an intentionally incorrect inverse version field
- **AND** the one-shot inverse version corruption SHALL be consumed
- **AND** later responses SHALL use normal inverse version unless another corruption is configured.

#### Scenario: 错误 payload length
- **GIVEN** fault injection is enabled
- **AND** `corruptNextDoipHeader.payloadLengthDelta` is non-zero
- **WHEN** the next eligible DoIP response is encoded for sending
- **THEN** the response SHALL contain a payload length adjusted by the configured delta
- **AND** the one-shot payload length corruption SHALL be consumed
- **AND** later responses SHALL use normal payload length unless another corruption is configured.

#### Scenario: 不实现复杂乱序
- **GIVEN** DoIP header corruption faults are implemented
- **WHEN** the implementation is inspected
- **THEN** the change SHALL NOT add packet reordering, frame shuffling, or multi-frame disorder orchestration.

### Requirement: Manual NRC And Custom UDS Response Faults

The system SHALL support overriding the next matching UDS service response with a manual NRC or custom UDS response.

#### Scenario: 下一次指定服务被手动 NRC 覆盖
- **GIVEN** fault injection is enabled
- **AND** a next NRC override is configured for UDS service `S`
- **AND** a client sends the next request for service `S`
- **WHEN** the UDS responder processes the request
- **THEN** the simulator SHALL return a UDS negative response for service `S` with the configured NRC
- **AND** the normal service handler response SHALL NOT be sent for that request
- **AND** the override SHALL be consumed after that response.

#### Scenario: 非指定 service 不消费 NRC 覆盖
- **GIVEN** a next NRC override is configured for UDS service `S`
- **WHEN** a client sends a request for a different UDS service
- **THEN** the simulator SHALL NOT apply the override to that different service
- **AND** the override for service `S` SHALL remain pending.

#### Scenario: 自定义 UDS 响应覆盖
- **GIVEN** fault injection is enabled
- **AND** a custom UDS response override is configured for service `S`
- **WHEN** the next request for service `S` is processed
- **THEN** the simulator SHALL send the configured raw UDS response bytes
- **AND** the override SHALL be consumed after being sent
- **AND** the system SHALL publish or log a custom UDS response override event.

### Requirement: Fault Runtime Events And Logs

The system SHALL publish diagnostic runtime events or logs for fault profile changes and triggered faults.

#### Scenario: 记录 fault 触发
- **GIVEN** a configured fault is triggered
- **WHEN** the simulator applies response delay, pause, disconnect, Routing Activation failure, DoIP header corruption, manual NRC, or custom UDS response
- **THEN** the system SHALL publish or log a fault event
- **AND** the event SHALL include the fault type
- **AND** it SHALL include the connection ID or service ID when available
- **AND** it SHALL avoid logging sensitive key material or unrelated payload secrets.

#### Scenario: Web 可观察 fault 事件
- **GIVEN** the WebConsole consumes runtime events or recent event APIs
- **WHEN** fault events are published
- **THEN** existing event views or the fault injection view SHALL be able to show a useful summary
- **AND** the change SHALL NOT require a separate realtime protocol.

### Requirement: Fault Injection Scope Boundaries

The task-024 implementation SHALL remain limited to the first batch of manually controlled, reproducible fault injection scenarios.

#### Scenario: 不做概率型策略编排
- **GIVEN** task-024 is implemented
- **WHEN** fault profile and Web controls are inspected
- **THEN** the change SHALL NOT add probabilistic scheduling, random failure percentages, weighted rule selection, or scenario orchestration engines.

#### Scenario: 不做所有 TLS 失败组合
- **GIVEN** task-024 is implemented
- **WHEN** TLS behavior is inspected
- **THEN** the change SHALL NOT implement all TLS failure combinations
- **AND** it SHALL NOT add certificate failure simulation beyond behavior already defined by prior TLS tasks.

#### Scenario: 不做长期故障脚本系统
- **GIVEN** task-024 is implemented
- **WHEN** WebApi, WebConsole, and runtime code are inspected
- **THEN** the change SHALL NOT add persistent fault scripts
- **AND** it SHALL NOT add script language execution
- **AND** it SHALL NOT add long-running scenario files or schedulers.

#### Scenario: 不扩大其他能力范围
- **GIVEN** task-024 is implemented
- **WHEN** the codebase is inspected
- **THEN** the change SHALL NOT add ODX/PDX import behavior
- **AND** it SHALL NOT add SecurityAccess DLL plugin behavior
- **AND** it SHALL NOT change normal UDS services except through explicit active fault overrides.

### Requirement: Fault Injection Verification

The task-024 implementation SHALL include focused verification for the first batch of fault injection behavior.

#### Scenario: fault profile 单元测试
- **GIVEN** automated unit tests validate fault profiles
- **WHEN** valid and invalid profiles are tested
- **THEN** the tests SHALL verify default disabled state
- **AND** they SHALL verify field-specific validation errors for invalid delay, NRC, service ID, payload length delta, and custom response bytes.

#### Scenario: 延迟、暂停、断连集成测试
- **GIVEN** automated integration tests run against a simulator instance
- **WHEN** response delay, pause responses, and manual disconnect are exercised
- **THEN** the tests SHALL verify client-observed delay
- **AND** they SHALL verify pause-induced timeout
- **AND** they SHALL verify target connection closure.

#### Scenario: DoIP header 和 Routing Activation 集成测试
- **GIVEN** automated integration tests run against a simulator instance
- **WHEN** Routing Activation failure and DoIP header corruption are exercised
- **THEN** the tests SHALL verify reproducible Routing Activation failure
- **AND** they SHALL verify incorrect inverse version
- **AND** they SHALL verify incorrect payload length.

#### Scenario: NRC 和自定义响应集成测试
- **GIVEN** automated integration tests run against a simulator instance
- **WHEN** next manual NRC and custom UDS response overrides are configured
- **THEN** the tests SHALL verify the next specified service can be overridden
- **AND** they SHALL verify the override is consumed after use
- **AND** they SHALL verify unrelated services are not overridden.

