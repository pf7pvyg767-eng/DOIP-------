# Spec: DID 运行时编辑和 `0x2E` WriteDataByIdentifier

**Change ID:** `task-015`
**Status:** Draft

---

## ADDED Requirements

### Requirement: DID Runtime Value Store

The system SHALL maintain current runtime values for configured fixed byte DIDs and expose them consistently to Web API, WebConsole, `0x22`, and `0x2E`.

#### Scenario: 运行时值覆盖配置初始值
- **GIVEN** DID `0xF190` is configured with an initial fixed hex value
- **WHEN** the simulator runtime starts
- **THEN** the DID runtime store SHALL expose DID `0xF190` with that current value
- **AND** `0x22` reads SHALL use the runtime store value rather than a stale copy

#### Scenario: Web 写入后读取使用新值
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** the DID runtime value is updated through the API
- **THEN** the runtime store SHALL replace the current value for DID `0xF190`
- **AND** subsequent `0x22` reads SHALL return the updated value

#### Scenario: `0x2E` 写入后 Web 使用新值
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** UDS `0x2E` writes a new value for DID `0xF190`
- **THEN** the runtime store SHALL replace the current value for DID `0xF190`
- **AND** `GET /api/dids` SHALL return the updated value

### Requirement: DID Write Configuration And Validation

The system SHALL extend DID configuration only as needed to describe writable fixed byte DIDs, write length, diagnostic session preconditions, and security state preconditions.

#### Scenario: 配置可写 DID
- **GIVEN** a simulator configuration contains a DID entry
- **WHEN** the entry marks DID `0xF190` as writable with `valueEncoding` set to `hex`
- **THEN** the configuration model SHALL expose the DID as writable
- **AND** the configured value SHALL remain a fixed hex byte string
- **AND** the configuration SHALL NOT require complex encoding conversion

#### Scenario: 保留只读 DID
- **GIVEN** a simulator configuration contains a DID entry that is not writable
- **WHEN** the configuration is loaded
- **THEN** the DID SHALL remain readable through existing `0x22` behavior when otherwise valid
- **AND** write attempts SHALL be rejected

#### Scenario: 拒绝无效写入值
- **GIVEN** a write request supplies a value for a configured DID
- **WHEN** the value is not an even-length hexadecimal byte string
- **THEN** the write SHALL be rejected
- **AND** the current DID runtime value SHALL remain unchanged

#### Scenario: 拒绝长度不匹配值
- **GIVEN** a configured DID has a fixed expected write length
- **WHEN** a write request supplies a value with a different byte length
- **THEN** the write SHALL be rejected
- **AND** the current DID runtime value SHALL remain unchanged

### Requirement: Dids List API

The system SHALL expose `GET /api/dids` to return the configured DID list with current runtime values.

#### Scenario: 返回 DID 列表
- **GIVEN** one or more DIDs are configured
- **WHEN** a caller sends `GET /api/dids`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL contain a JSON array of DID summaries
- **AND** each DID summary SHALL include DID ID, name when available, `valueEncoding`, current value, writable flag, expected length when available, and permission summary when available

#### Scenario: 当前值更新后列表反映新值
- **GIVEN** DID `0xF190` has been updated through API or UDS `0x2E`
- **WHEN** a caller sends `GET /api/dids`
- **THEN** the response SHALL include DID `0xF190`
- **AND** the returned value SHALL equal the updated runtime value

### Requirement: DID Value Update API

The system SHALL expose `PUT /api/dids/{did}/value` to update a writable DID runtime value.

#### Scenario: API 写入成功
- **GIVEN** DID `0xF190` is configured and writable
- **AND** the current diagnostic session and security state satisfy its write preconditions
- **WHEN** a caller sends `PUT /api/dids/F190/value` with `valueEncoding` set to `hex` and a valid value
- **THEN** the response SHALL indicate success
- **AND** the DID runtime value SHALL be updated
- **AND** a subsequent `GET /api/dids` SHALL return the updated value

#### Scenario: API 写入后 `0x22` 立即读到新值
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** `PUT /api/dids/F190/value` successfully writes value `01020304`
- **THEN** a subsequent UDS `0x22 F1 90` request SHALL return a positive response beginning with `0x62, 0xF1, 0x90`
- **AND** the response bytes after the DID SHALL equal `0x01, 0x02, 0x03, 0x04`

#### Scenario: API 拒绝不可写 DID
- **GIVEN** DID `0xF190` is configured but not writable
- **WHEN** a caller sends `PUT /api/dids/F190/value` with a valid value
- **THEN** the API SHALL reject the request
- **AND** the DID runtime value SHALL remain unchanged

#### Scenario: API 仅支持 hex 编码
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** a caller sends `PUT /api/dids/F190/value` with `valueEncoding` other than `hex`
- **THEN** the API SHALL reject the request
- **AND** the implementation SHALL NOT perform complex encoding conversion

### Requirement: DID JSON Persistence

The system SHALL persist DID configuration and values to JSON when requested.

#### Scenario: `persist=true` 写回 JSON
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** a caller successfully writes a new value with `persist` set to `true`
- **THEN** the runtime value SHALL be updated
- **AND** the JSON configuration SHALL be saved with the new DID value

#### Scenario: 持久化后重启保留新值
- **GIVEN** DID `0xF190` was successfully written with `persist` set to `true`
- **WHEN** the simulator restarts or reloads configuration from JSON
- **THEN** DID `0xF190` SHALL retain the new value
- **AND** `0x22 F1 90` SHALL return the retained value

#### Scenario: `persist=false` 不写回 JSON
- **GIVEN** DID `0xF190` is configured and writable
- **WHEN** a caller successfully writes a new value with `persist` set to `false`
- **THEN** the runtime value SHALL be updated for the running simulator
- **AND** the JSON configuration SHALL NOT be required to contain the new value

### Requirement: WriteDataByIdentifier Service Registration

The UDS protocol layer SHALL register service `0x2E` WriteDataByIdentifier with the existing UDS dispatcher.

#### Scenario: 注册 `0x2E` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x2E` SHALL be handled by a WriteDataByIdentifier service
- **AND** unsupported service behavior for other service IDs SHALL remain unchanged

#### Scenario: DoIP 层不实现 DID 写入业务逻辑
- **GIVEN** a DoIP diagnostic message is received after Routing Activation
- **WHEN** the UDS payload service ID is `0x2E`
- **THEN** the DoIP layer SHALL forward the payload to the UDS dispatcher
- **AND** the DoIP layer SHALL NOT parse writable DID configuration or mutate DID values itself

### Requirement: WriteDataByIdentifier Positive Response

The WriteDataByIdentifier service SHALL update writable DID runtime values and return positive response `0x6E DID` on success.

#### Scenario: `0x2E` 写入 DID 成功
- **GIVEN** DID `0xF190` is configured and writable
- **AND** the current diagnostic session and security state satisfy its write preconditions
- **WHEN** the service receives UDS request bytes `0x2E, 0xF1, 0x90, 0x01, 0x02, 0x03, 0x04`
- **THEN** the service SHALL return a positive response
- **AND** the response SHALL equal bytes `0x6E, 0xF1, 0x90`
- **AND** the DID runtime value SHALL become `0x01, 0x02, 0x03, 0x04`

#### Scenario: `0x2E` 后 `0x22` 验证
- **GIVEN** DID `0xF190` was successfully written through `0x2E`
- **WHEN** the service subsequently receives UDS request bytes `0x22, 0xF1, 0x90`
- **THEN** the ReadDataByIdentifier service SHALL return a positive response beginning with `0x62, 0xF1, 0x90`
- **AND** the response value bytes SHALL equal the value written by `0x2E`

### Requirement: WriteDataByIdentifier Request Validation

The WriteDataByIdentifier service SHALL validate request format and DID value length before mutating runtime state.

#### Scenario: 缺少 DID 返回长度格式错误
- **GIVEN** the WriteDataByIdentifier service receives service ID `0x2E` with fewer than two DID bytes
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat`
- **AND** no DID runtime value SHALL be changed

#### Scenario: 缺少写入数据返回长度格式错误
- **GIVEN** the WriteDataByIdentifier service receives service ID `0x2E` and a complete DID but no data bytes
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat`
- **AND** no DID runtime value SHALL be changed

#### Scenario: 写入长度错误返回长度格式错误
- **GIVEN** DID `0xF190` is configured with an expected write length
- **WHEN** the service receives a value whose byte length does not match the expected length
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat`
- **AND** no DID runtime value SHALL be changed

### Requirement: WriteDataByIdentifier Permission Checks

The WriteDataByIdentifier service SHALL enforce configured write permission preconditions for diagnostic session and security state.

#### Scenario: 不可写 DID 返回 `0x31`
- **GIVEN** DID `0xF190` is configured but not writable
- **WHEN** the service receives a syntactically valid `0x2E` request for DID `0xF190`
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x31 RequestOutOfRange`
- **AND** no DID runtime value SHALL be changed

#### Scenario: 未配置 DID 返回 `0x31`
- **GIVEN** DID `0xF199` is not configured
- **WHEN** the service receives a syntactically valid `0x2E` request for DID `0xF199`
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x31 RequestOutOfRange`
- **AND** no DID runtime value SHALL be changed

#### Scenario: 会话前置条件不满足返回 `0x22`
- **GIVEN** DID `0xF190` requires an extended or programming diagnostic session for writing
- **AND** the current diagnostic session does not satisfy that requirement
- **WHEN** the service receives a syntactically valid `0x2E` request for DID `0xF190`
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x22 ConditionsNotCorrect`
- **AND** no DID runtime value SHALL be changed

#### Scenario: 安全状态前置条件不满足返回 `0x33`
- **GIVEN** DID `0xF190` requires an unlocked security state for writing
- **AND** the current security state does not satisfy that requirement
- **WHEN** the service receives a syntactically valid `0x2E` request for DID `0xF190`
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x33 SecurityAccessDenied`
- **AND** no DID runtime value SHALL be changed

### Requirement: DID Edit WebConsole

The WebConsole SHALL provide a DID list and runtime value editing UI backed by the DID API.

#### Scenario: 显示 DID 列表
- **GIVEN** the WebConsole DID view is opened
- **WHEN** `GET /api/dids` returns DID summaries
- **THEN** the UI SHALL display the DID list
- **AND** each row SHALL show DID ID, name when available, current hex value, writable state, expected length, and permission summary when available

#### Scenario: 编辑可写 DID
- **GIVEN** a DID row is writable
- **WHEN** the user submits a valid hex value
- **THEN** the UI SHALL call `PUT /api/dids/{did}/value`
- **AND** after success the UI SHALL refresh or update the row with the new value

#### Scenario: 禁止写错误可见
- **GIVEN** a DID write is rejected by the API
- **WHEN** the UI receives the error response
- **THEN** the UI SHALL NOT display the write as successful
- **AND** the UI SHALL present a clear error state for the row or form

### Requirement: DID Write Runtime Events

The system SHALL publish or record runtime information for successful DID writes through the existing runtime event approach when available.

#### Scenario: API 写入事件包含 DID 和长度
- **GIVEN** a DID value is successfully updated through `PUT /api/dids/{did}/value`
- **WHEN** the runtime event is published or recorded
- **THEN** the event data SHALL include the DID ID
- **AND** the event data SHALL include the new value length
- **AND** the event data SHALL identify the source as API or Web when available

#### Scenario: UDS 写入事件包含 DID 和长度
- **GIVEN** a DID value is successfully updated through UDS `0x2E`
- **WHEN** the runtime event is published or recorded
- **THEN** the event data SHALL include the DID ID
- **AND** the event data SHALL include the new value length
- **AND** the event data SHALL identify the source as UDS when available

### Requirement: Scope Boundaries

The implementation SHALL remain limited to fixed hex DID runtime editing, DID APIs, JSON persistence, and UDS `0x2E` WriteDataByIdentifier.

#### Scenario: 不支持复杂编码转换
- **GIVEN** task-015 is implemented
- **WHEN** DID write API and WebConsole controls are inspected
- **THEN** the change SHALL NOT add VIN-specific string conversion
- **AND** it SHALL NOT add decimal, base64, endian conversion, scaling, script, or business semantic encoding

#### Scenario: 不支持 ODX 写入定义
- **GIVEN** task-015 is implemented
- **WHEN** configuration and import code are inspected
- **THEN** it SHALL NOT add ODX write definition parsing
- **AND** it SHALL NOT add PDX import or diagnostic database conversion

#### Scenario: 不支持动态 DID
- **GIVEN** task-015 is implemented
- **WHEN** DID configuration and UDS services are inspected
- **THEN** the change SHALL NOT add dynamic DID definition, dynamic DID composition, or dynamic DID runtime registration

#### Scenario: 不扩大到其他诊断业务
- **GIVEN** task-015 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement DTC services
- **AND** it SHALL NOT implement Routine services
- **AND** it SHALL NOT implement Flash services
- **AND** it SHALL NOT implement SecurityAccess seed/key or unlock behavior
- **AND** SecurityAccess usage SHALL be limited to reading existing security state for DID write permission checks

## ADDED Requirements

### Requirement: Fixed Byte DID Configuration

The system SHALL extend the existing fixed byte DID configuration from task-013 to support optional write metadata while preserving readable fixed byte DID behavior.

#### Scenario: 既有只读 DID 继续可读
- **GIVEN** a DID configuration created for task-013 without write metadata
- **WHEN** task-015 is implemented
- **THEN** the DID SHALL continue to be readable by `0x22`
- **AND** it SHALL NOT become writable by default unless explicitly configured as writable

### Requirement: Single DID Read Positive Response

The ReadDataByIdentifier service SHALL return the current runtime value for a configured DID, including values updated by API or UDS `0x2E`.

#### Scenario: `0x22` 返回运行时更新后的值
- **GIVEN** DID `0xF190` is configured and its runtime value was updated after startup
- **WHEN** the service receives UDS request bytes `0x22, 0xF1, 0x90`
- **THEN** the service SHALL return a positive response beginning with `0x62, 0xF1, 0x90`
- **AND** the response value bytes SHALL equal the current runtime value

