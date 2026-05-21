# dtc-services-web-injection Specification

## Purpose
TBD - created by archiving change task-016. Update Purpose after archive.
## Requirements
### Requirement: DTC Configuration Model

The system SHALL define a minimal DTC configuration model for configured diagnostic trouble codes without importing ODX DTC definitions.

#### Scenario: 配置固定 DTC
- **GIVEN** a simulator configuration contains a DTC entry with code `0x123456`
- **WHEN** the configuration is loaded
- **THEN** the DTC configuration SHALL expose the DTC code as a 24-bit identifier
- **AND** it SHALL expose the configured status byte
- **AND** it MAY expose a name or description for Web display
- **AND** it SHALL NOT require ODX or external diagnostic database input

#### Scenario: 拒绝无效 DTC code
- **GIVEN** a simulator configuration contains an invalid DTC code
- **WHEN** the configuration is validated or the runtime store is initialized
- **THEN** the operation SHALL fail with a clear error identifying the DTC code field
- **AND** the simulator SHALL NOT create an ambiguous runtime DTC entry

#### Scenario: 不建立完整故障状态机
- **GIVEN** task-016 is implemented
- **WHEN** DTC configuration is inspected
- **THEN** the model SHALL remain limited to MVP fields such as code, status, active flag, name, and description
- **AND** it SHALL NOT require aging counters, confirmation counters, test-failed transition rules, or full ECU failure lifecycle configuration

### Requirement: DTC Runtime Store

The system SHALL maintain current DTC runtime state in a shared store used by Web API, WebConsole, `0x19`, and `0x14`.

#### Scenario: 初始化 DTC runtime state
- **GIVEN** one or more DTC entries are configured
- **WHEN** the simulator runtime starts
- **THEN** the DTC runtime store SHALL expose each configured DTC by code
- **AND** each entry SHALL include current active state and status byte
- **AND** the initial runtime state SHALL come from configuration defaults or explicit initial fields

#### Scenario: 激活已配置 DTC
- **GIVEN** DTC `0x123456` exists in the runtime store
- **WHEN** the DTC is activated through Web API or another approved runtime path
- **THEN** the runtime store SHALL mark DTC `0x123456` as active
- **AND** it SHALL retain or update the DTC status according to the request and configured defaults
- **AND** subsequent DTC snapshots SHALL show the active state

#### Scenario: 清除已配置 DTC
- **GIVEN** DTC `0x123456` is active in the runtime store
- **WHEN** the DTC is cleared through Web API or UDS `0x14`
- **THEN** the runtime store SHALL mark DTC `0x123456` as cleared or inactive
- **AND** subsequent DTC snapshots SHALL reflect the cleared result

#### Scenario: 未知 DTC 操作不改变状态
- **GIVEN** DTC `0x654321` is not configured in the runtime store
- **WHEN** a caller attempts to activate or clear DTC `0x654321`
- **THEN** the operation SHALL return a clear unknown DTC error
- **AND** no configured DTC runtime state SHALL be changed

### Requirement: DTC Web API

The system SHALL expose Web API endpoints for listing, activating, injecting, and clearing configured DTC runtime state.

#### Scenario: 返回 DTC 列表
- **GIVEN** one or more DTC entries exist in the runtime store
- **WHEN** a caller sends `GET /api/dtcs`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL contain a JSON array of DTC summaries
- **AND** each summary SHALL include DTC code, status, active state, and description or name when available

#### Scenario: Web 激活 DTC
- **GIVEN** DTC `0x123456` is configured
- **WHEN** a caller sends `POST /api/dtcs/123456/activate`
- **THEN** the API SHALL activate DTC `0x123456` in the runtime store
- **AND** the response SHALL return the updated DTC summary
- **AND** a subsequent `GET /api/dtcs` SHALL show DTC `0x123456` as active

#### Scenario: Web 注入 status
- **GIVEN** DTC `0x123456` is configured
- **WHEN** a caller activates it with an allowed status byte in the request body
- **THEN** the runtime store SHALL use that status byte for the active DTC
- **AND** the returned DTC summary SHALL include the updated status

#### Scenario: Web 清除 DTC
- **GIVEN** DTC `0x123456` is active
- **WHEN** a caller sends `POST /api/dtcs/123456/clear`
- **THEN** the API SHALL clear DTC `0x123456` in the runtime store
- **AND** a subsequent `GET /api/dtcs` SHALL show the cleared result

#### Scenario: Web 操作未知 DTC 返回明确错误
- **GIVEN** DTC `0x654321` is not configured
- **WHEN** a caller sends `POST /api/dtcs/654321/activate` or `POST /api/dtcs/654321/clear`
- **THEN** the API SHALL return a non-success HTTP response
- **AND** the response body SHALL include a clear unknown DTC error
- **AND** no configured DTC runtime state SHALL be changed

### Requirement: DTC WebConsole

The WebConsole SHALL provide DTC list, activation/injection, and clear controls backed by the DTC Web API.

#### Scenario: 显示 DTC 列表
- **GIVEN** the WebConsole DTC view or panel is opened
- **WHEN** `GET /api/dtcs` returns DTC summaries
- **THEN** the UI SHALL display configured DTCs
- **AND** each row SHALL show code, active state, status, and description or name when available

#### Scenario: 激活 DTC 后刷新显示
- **GIVEN** DTC `0x123456` is shown in the WebConsole
- **WHEN** the user activates or injects that DTC
- **THEN** the UI SHALL call the DTC activation API
- **AND** after success the UI SHALL show DTC `0x123456` as active with its current status

#### Scenario: 清除 DTC 后刷新显示
- **GIVEN** DTC `0x123456` is active in the WebConsole
- **WHEN** the user clears that DTC
- **THEN** the UI SHALL call the DTC clear API
- **AND** after success the UI SHALL show the cleared result

#### Scenario: 未知 DTC 错误可见
- **GIVEN** a DTC Web operation is rejected as unknown or invalid
- **WHEN** the UI receives the error response
- **THEN** the UI SHALL NOT display the operation as successful
- **AND** the UI SHALL present a clear error state for the row or form

### Requirement: ReadDTCInformation MVP Service

The UDS protocol layer SHALL register service `0x19` ReadDTCInformation and implement only the MVP subset needed to read current runtime DTC state.

#### Scenario: 注册 `0x19` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x19` SHALL be handled by a ReadDTCInformation service
- **AND** unsupported service behavior for unrelated service IDs SHALL remain unchanged

#### Scenario: Web 激活后 `0x19` 可读取
- **GIVEN** DTC `0x123456` is configured
- **AND** Web API has activated DTC `0x123456` with status `0x2F`
- **WHEN** the ReadDTCInformation MVP service receives a supported `0x19` request for current DTCs
- **THEN** it SHALL return a positive response
- **AND** the response SHALL include DTC `0x123456`
- **AND** the response SHALL include status `0x2F` for that DTC

#### Scenario: `0x19` 查询反映清除结果
- **GIVEN** DTC `0x123456` was active and then cleared
- **WHEN** the ReadDTCInformation MVP service receives a supported `0x19` request for current DTCs
- **THEN** the response SHALL reflect the cleared runtime state
- **AND** DTC `0x123456` SHALL NOT be reported as active

#### Scenario: 未支持 `0x19` 子功能返回明确 NRC
- **GIVEN** the ReadDTCInformation service receives a `0x19` subfunction outside the MVP subset
- **WHEN** the request is validated
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL clearly indicate unsupported subfunction or request out of range
- **AND** the service SHALL NOT synthesize unsupported DTC data

#### Scenario: `0x19` 请求格式错误不改变 DTC state
- **GIVEN** the ReadDTCInformation service receives a malformed request
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response using a clear format or length NRC
- **AND** no DTC runtime state SHALL be changed

### Requirement: ClearDiagnosticInformation Service

The UDS protocol layer SHALL register service `0x14` ClearDiagnosticInformation and clear matching DTC runtime state through the shared DTC runtime store.

#### Scenario: 注册 `0x14` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x14` SHALL be handled by a ClearDiagnosticInformation service
- **AND** unsupported service behavior for unrelated service IDs SHALL remain unchanged

#### Scenario: `0x14` 清除单个 DTC
- **GIVEN** DTC `0x123456` is active in the runtime store
- **WHEN** the ClearDiagnosticInformation service receives a supported `0x14` request targeting DTC `0x123456`
- **THEN** it SHALL clear DTC `0x123456` in the runtime store
- **AND** it SHALL return a positive response for service `0x14`
- **AND** subsequent `GET /api/dtcs` SHALL show DTC `0x123456` as cleared
- **AND** subsequent supported `0x19` query SHALL reflect the cleared result

#### Scenario: `0x14` 清除后 Web 和 `0x19` 一致
- **GIVEN** Web API shows DTC `0x123456` as active
- **WHEN** UDS `0x14` clears DTC `0x123456`
- **THEN** `GET /api/dtcs` SHALL reflect the cleared result
- **AND** a supported `0x19` request SHALL reflect the same cleared result

#### Scenario: `0x14` 操作未知 DTC 返回明确错误
- **GIVEN** DTC `0x654321` is not configured
- **WHEN** the ClearDiagnosticInformation service receives a request targeting DTC `0x654321`
- **THEN** it SHALL return a negative response with a clear NRC such as `0x31 RequestOutOfRange`
- **AND** no configured DTC runtime state SHALL be changed

#### Scenario: `0x14` 请求格式错误返回长度格式 NRC
- **GIVEN** the ClearDiagnosticInformation service receives a request without a complete DTC or supported group parameter
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat` or an equivalent clear format NRC
- **AND** no DTC runtime state SHALL be changed

### Requirement: DTC Runtime Events And Logs

The system SHALL publish and log DTC runtime events for activation, clear, successful reads, and rejected operations through the existing runtime event pipeline.

#### Scenario: 激活事件进入日志
- **GIVEN** DTC `0x123456` is activated through Web API
- **WHEN** the runtime event is published
- **THEN** the event SHALL identify DTC `0x123456`
- **AND** the event SHALL identify the operation as activation or injection
- **AND** the event SHALL include active/status summary
- **AND** the event SHALL be visible through the existing structured log path

#### Scenario: 清除事件进入日志
- **GIVEN** DTC `0x123456` is cleared through Web API or UDS `0x14`
- **WHEN** the runtime event is published
- **THEN** the event SHALL identify DTC `0x123456`
- **AND** the event SHALL identify the operation as clear
- **AND** the event SHALL include the source as Web/API or UDS when available
- **AND** the event SHALL be visible through the existing structured log path

#### Scenario: 查询事件进入日志
- **GIVEN** a supported `0x19` request reads active DTC state
- **WHEN** the runtime event is published
- **THEN** the event SHALL identify the operation as DTC read
- **AND** it SHALL include a count or summary of DTCs returned
- **AND** it SHALL be visible through the existing structured log path

#### Scenario: 未知 DTC 错误进入日志
- **GIVEN** Web API or UDS receives an operation for an unknown DTC
- **WHEN** the operation is rejected
- **THEN** the runtime event or log entry SHALL include the unknown DTC code
- **AND** it SHALL include the rejected operation name
- **AND** it SHALL not require the simulator to stop

### Requirement: DTC DoIP Diagnostic Integration

The existing DoIP diagnostic forwarding path SHALL route `0x19` and `0x14` requests through the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后读取 DTC
- **GIVEN** a TCP client has completed Routing Activation
- **AND** DTC `0x123456` is active
- **WHEN** the client sends a DoIP diagnostic message with a supported UDS `0x19` payload
- **THEN** the payload SHALL be dispatched to the ReadDTCInformation service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x19` response

#### Scenario: Routing Activation 后清除 DTC
- **GIVEN** a TCP client has completed Routing Activation
- **AND** DTC `0x123456` is active
- **WHEN** the client sends a DoIP diagnostic message with a supported UDS `0x14` payload for that DTC
- **THEN** the payload SHALL be dispatched to the ClearDiagnosticInformation service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x14` response
- **AND** the DTC runtime store SHALL reflect the clear

#### Scenario: DoIP 层不实现 DTC 业务
- **GIVEN** task-016 is implemented
- **WHEN** the DoIP diagnostic message handler is inspected
- **THEN** it SHALL continue forwarding UDS payloads to the dispatcher
- **AND** it SHALL NOT parse DTC configuration
- **AND** it SHALL NOT mutate DTC runtime state directly

### Requirement: Scope Boundaries

The implementation SHALL remain limited to DTC configuration/runtime store, Web DTC injection/activation/clear, `0x19` MVP, `0x14`, and DTC events/logs.

#### Scenario: 不覆盖 `0x19` 全部子功能
- **GIVEN** task-016 is implemented
- **WHEN** the ReadDTCInformation service is inspected
- **THEN** it SHALL only implement the MVP subset documented for this change
- **AND** unsupported `0x19` subfunctions SHALL return clear negative responses

#### Scenario: 不实现真实 DTC 完整状态机
- **GIVEN** task-016 is implemented
- **WHEN** DTC runtime behavior is inspected
- **THEN** it SHALL NOT implement aging
- **AND** it SHALL NOT implement confirmation lifecycle
- **AND** it SHALL NOT implement a full test-failed state machine
- **AND** it SHALL NOT simulate monitor execution logic

#### Scenario: 不导入 ODX DTC
- **GIVEN** task-016 is implemented
- **WHEN** configuration and import code are inspected
- **THEN** it SHALL NOT add ODX DTC import
- **AND** it SHALL NOT add PDX diagnostic database conversion

#### Scenario: 不扩大到其他诊断流程
- **GIVEN** task-016 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement SecurityAccess flows
- **AND** it SHALL NOT implement RoutineControl flows
- **AND** it SHALL NOT implement Flash flows
- **AND** it SHALL NOT add unrelated diagnostic service behavior beyond `0x19` and `0x14`

