# Spec: P2/P2*、TesterPresent 超时和 ResponsePending
**Change ID:** `task-019`
**Status:** Implemented

---

## ADDED Requirements

### Requirement: Timing Configuration

The system SHALL support deterministic timing configuration for diagnostic session P2/P2*, TesterPresent timeout, and service-level response delay.

#### Scenario: 加载会话 P2/P2* 配置
- **GIVEN** a simulator configuration contains P2 and P2* parameters for a diagnostic session
- **WHEN** the configuration is loaded
- **THEN** the session timing configuration SHALL expose the configured P2 value
- **AND** it SHALL expose the configured P2* value
- **AND** the values SHALL be available to DiagnosticSessionControl service `0x10`

#### Scenario: 加载 TesterPresent 超时配置
- **GIVEN** a simulator configuration contains TesterPresent timeout settings
- **WHEN** the configuration is loaded
- **THEN** the runtime SHALL expose whether TesterPresent timeout monitoring is enabled
- **AND** it SHALL expose the configured fixed timeout duration

#### Scenario: 加载服务级响应延迟配置
- **GIVEN** a simulator configuration contains a response delay entry for service `0x31`
- **WHEN** the configuration is loaded
- **THEN** the entry SHALL expose service ID `0x31`
- **AND** it SHALL expose whether ResponsePending is enabled
- **AND** it SHALL expose `initialDelayMs`
- **AND** it SHALL expose `finalDelayMs`

#### Scenario: 拒绝无效定时配置
- **GIVEN** a simulator configuration contains an invalid P2/P2* value, invalid TesterPresent timeout, invalid service ID, or negative delay value
- **WHEN** the configuration is validated
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the invalid timing field

### Requirement: TesterPresent Timeout Session Fallback

The system SHALL fall back to default diagnostic session after TesterPresent timeout when timeout monitoring is enabled.

#### Scenario: TesterPresent 未超时时保持当前会话
- **GIVEN** TesterPresent timeout monitoring is enabled
- **AND** the ECU is in an extended or programming session
- **AND** the most recent accepted TesterPresent is still within the configured timeout duration
- **WHEN** timeout evaluation runs
- **THEN** the ECU SHALL remain in the current diagnostic session
- **AND** no timeout fallback event SHALL be emitted

#### Scenario: TesterPresent 超时后回退默认会话
- **GIVEN** TesterPresent timeout monitoring is enabled
- **AND** the ECU is in an extended or programming session
- **AND** no accepted TesterPresent has been observed within the configured timeout duration
- **WHEN** timeout evaluation runs
- **THEN** the ECU SHALL switch to the default diagnostic session
- **AND** the runtime state SHALL record the fallback

#### Scenario: 超时回退产生日志或事件
- **GIVEN** TesterPresent timeout causes a session fallback
- **WHEN** the runtime state is updated
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the entry SHALL include the old session
- **AND** it SHALL include the new default session
- **AND** it SHALL identify TesterPresent timeout as the reason

#### Scenario: 默认会话超时不产生错误切换
- **GIVEN** TesterPresent timeout monitoring is enabled
- **AND** the ECU is already in the default diagnostic session
- **WHEN** timeout evaluation runs after the configured timeout duration
- **THEN** the ECU SHALL remain in the default session
- **AND** the implementation SHALL NOT emit a misleading session fallback event

### Requirement: Configured P2 And P2* In DiagnosticSessionControl

DiagnosticSessionControl positive responses SHALL include configured P2 and P2* parameters.

#### Scenario: `0x10` 默认会话响应返回配置 P2/P2*
- **GIVEN** the default diagnostic session has configured P2 and P2* values
- **WHEN** the DiagnosticSessionControl service receives request `0x10, 0x01`
- **THEN** the positive response SHALL start with `0x50, 0x01`
- **AND** the response SHALL encode the configured P2 value
- **AND** the response SHALL encode the configured P2* value

#### Scenario: `0x10` 扩展会话响应返回配置 P2/P2*
- **GIVEN** the extended diagnostic session has configured P2 and P2* values
- **WHEN** the DiagnosticSessionControl service receives request `0x10, 0x03`
- **THEN** the positive response SHALL start with `0x50, 0x03`
- **AND** the response SHALL encode the configured P2 value
- **AND** the response SHALL encode the configured P2* value

#### Scenario: `0x10` 编程会话响应返回配置 P2/P2*
- **GIVEN** the programming diagnostic session has configured P2 and P2* values
- **WHEN** the DiagnosticSessionControl service receives request `0x10, 0x02`
- **THEN** the positive response SHALL start with `0x50, 0x02`
- **AND** the response SHALL encode the configured P2 value
- **AND** the response SHALL encode the configured P2* value

#### Scenario: 未配置时使用明确默认 P2/P2*
- **GIVEN** a diagnostic session does not override P2 or P2*
- **WHEN** the DiagnosticSessionControl service returns a positive response for that session
- **THEN** the response SHALL include deterministic default P2 and P2* values
- **AND** the defaults SHALL be stable in automated tests

### Requirement: Service-Level Response Delay

The UDS response path SHALL support deterministic service-level response delay configuration without probabilistic behavior.

#### Scenario: 未配置延迟的服务保持原行为
- **GIVEN** service `0x22` has no response delay configuration
- **WHEN** service `0x22` returns a response
- **THEN** the response SHALL be sent using the existing response path
- **AND** the implementation SHALL NOT emit ResponsePending for that service

#### Scenario: 配置最终延迟但未启用 ResponsePending
- **GIVEN** service `0x31` has a fixed final response delay configured
- **AND** ResponsePending is disabled for service `0x31`
- **WHEN** service `0x31` handles a valid request
- **THEN** the final response SHALL be sent after the configured deterministic delay
- **AND** no `0x78 ResponsePending` response SHALL be sent

#### Scenario: 延迟配置不使用概率行为
- **GIVEN** service-level response delay is configured
- **WHEN** the response delay is evaluated
- **THEN** the selected delay SHALL be the configured deterministic value
- **AND** the implementation SHALL NOT apply random jitter
- **AND** it SHALL NOT apply probability-based delay selection

### Requirement: ResponsePending Sequence

When ResponsePending is enabled for a service, the system SHALL send `0x78 ResponsePending` before the final service response.

#### Scenario: ResponsePending 后返回最终响应
- **GIVEN** service `0x31` is configured with ResponsePending enabled
- **AND** `initialDelayMs` is configured as a fixed deterministic value
- **AND** `finalDelayMs` is configured as a fixed deterministic value
- **WHEN** service `0x31` handles a request that has a final positive response
- **THEN** the first UDS response SHALL be negative response `0x7F, 0x31, 0x78`
- **AND** the later UDS response SHALL be the final positive response for service `0x31`
- **AND** the final response SHALL preserve the original service payload semantics

#### Scenario: ResponsePending 后返回最终负响应
- **GIVEN** service `0x31` is configured with ResponsePending enabled
- **WHEN** service `0x31` handles a request that has a final negative response other than `0x78`
- **THEN** the first UDS response SHALL be negative response `0x7F, 0x31, 0x78`
- **AND** the later UDS response SHALL be the final negative response determined by the service
- **AND** the final response SHALL NOT be replaced by `0x78`

#### Scenario: 响应顺序稳定
- **GIVEN** a service is configured with ResponsePending enabled
- **WHEN** a diagnostic request is processed
- **THEN** the client SHALL observe ResponsePending before the final response
- **AND** the final response SHALL NOT be sent before ResponsePending for that request

### Requirement: Non-Blocking Timing Behavior

Timing behavior SHALL NOT block basic processing for other diagnostic connections.

#### Scenario: 一个连接等待最终响应时其他连接仍可处理
- **GIVEN** connection A has sent a request to a service configured with ResponsePending and final delay
- **AND** connection A is waiting for the final response
- **WHEN** connection B sends a basic supported diagnostic request
- **THEN** connection B SHALL receive its response without waiting for connection A final delay to complete
- **AND** connection A SHALL still receive its final response in the configured order

#### Scenario: 延迟任务可取消
- **GIVEN** a delayed final response is pending for a connection
- **WHEN** the connection is closed before the final response is sent
- **THEN** the pending response operation SHALL be cancelled or safely completed without crashing the simulator
- **AND** it SHALL NOT block later requests from other connections

### Requirement: Web Timing Status Display

The Web/API observation surface SHALL expose read-only timing status.

#### Scenario: API 返回定时状态摘要
- **GIVEN** the simulator has ECU timing state available
- **WHEN** the ECU state snapshot API is requested
- **THEN** the response SHALL include read-only timing status
- **AND** the status SHALL include the latest TesterPresent timestamp when available
- **AND** it SHALL include TesterPresent timeout or fallback state when available
- **AND** it SHALL NOT expose secret key material or mutable timing control operations

#### Scenario: Web 展示定时状态
- **GIVEN** the Web console receives ECU timing status from the API or event stream
- **WHEN** the ECU state panel is rendered
- **THEN** it SHALL display the timing status summary
- **AND** it SHALL show enough information to identify current session and TesterPresent timeout status
- **AND** it SHALL NOT provide controls to edit timing configuration

### Requirement: Scope Boundaries

The implementation SHALL remain limited to deterministic MVP timing behavior for task-019.

#### Scenario: 不实现复杂调度器
- **GIVEN** task-019 is implemented
- **WHEN** the timing implementation is inspected
- **THEN** it SHALL use a lightweight deterministic timing mechanism
- **AND** it SHALL NOT introduce a general-purpose job scheduler or policy engine

#### Scenario: 不实现概率型延迟
- **GIVEN** task-019 is implemented
- **WHEN** response delay behavior is inspected
- **THEN** it SHALL NOT use random delay selection
- **AND** it SHALL NOT use probability weights or jitter configuration

#### Scenario: 不实现完整 OEM 时序策略
- **GIVEN** task-019 is implemented
- **WHEN** changed behavior is inspected
- **THEN** it SHALL NOT implement OEM-specific timing strategy tables
- **AND** it SHALL NOT add Flash timing phases
- **AND** it SHALL NOT add ISO-TP multi-frame timing behavior
- **AND** it SHALL NOT add unrelated diagnostic flows

## ADDED Requirements

### Requirement: Baseline P2 And P2* Parameters

DiagnosticSessionControl positive responses SHALL include configured P2 and P2* parameters when configured, and deterministic defaults otherwise.

#### Scenario: 返回配置或默认 P2/P2* 参数
- **GIVEN** DiagnosticSessionControl accepts a supported session sub-function
- **WHEN** the positive response is generated
- **THEN** the response SHALL include P2 and P2* parameter bytes after the echoed sub-function
- **AND** those bytes SHALL come from session configuration when present
- **AND** those bytes SHALL fall back to deterministic defaults when configuration is absent

### Requirement: TesterPresent Service

TesterPresent service `0x3E` SHALL refresh the runtime timeout state when a valid TesterPresent request is accepted.

#### Scenario: TesterPresent 刷新超时截止
- **GIVEN** TesterPresent timeout monitoring is enabled
- **WHEN** the TesterPresent service accepts request `0x3E, 0x00`
- **THEN** the ECU runtime state SHALL record the accepted TesterPresent time
- **AND** it SHALL refresh the TesterPresent timeout deadline based on the configured timeout duration

