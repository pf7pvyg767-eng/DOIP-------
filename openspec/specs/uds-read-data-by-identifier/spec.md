# uds-read-data-by-identifier Specification

## Purpose
TBD - created by archiving change task-013. Update Purpose after archive.
## Requirements
### Requirement: Fixed Byte DID Configuration
The system SHALL preserve fixed byte DID configuration and additionally allow DIDs to describe built-in dynamic value providers.

#### Scenario: 配置固定字节 DID
- **GIVEN** a simulator configuration contains a DID entry
- **WHEN** the DID entry describes DID `0xF190` with hex value encoding and a hex byte string value
- **THEN** the configuration model SHALL expose the DID as a 16-bit DID identifier
- **AND** the configured value SHALL be available as fixed response bytes
- **AND** the model SHALL NOT require dynamic provider configuration

#### Scenario: 保留 DID 名称
- **GIVEN** a DID configuration contains a human-readable name
- **WHEN** the configuration is loaded and saved
- **THEN** the name SHALL be preserved
- **AND** the name SHALL NOT affect the encoded UDS response bytes

#### Scenario: 拒绝无效 DID ID
- **GIVEN** a simulator configuration contains a DID entry with an invalid DID ID
- **WHEN** the configuration validator validates the configuration
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the DID ID field

#### Scenario: 拒绝无效固定字节值
- **GIVEN** a simulator configuration contains a static DID entry with `valueEncoding` set to `hex`
- **WHEN** the configured value is not an even-length hexadecimal byte string
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the DID value field

#### Scenario: 支持内置动态 provider 但不支持脚本表达式
- **GIVEN** a simulator configuration contains a DID entry with `valueProvider.type` set to `random`, `sine`, or `linear`
- **WHEN** DID configuration is inspected
- **THEN** the configuration MAY compute DID values from built-in numeric providers
- **AND** it SHALL NOT execute scripts
- **AND** it SHALL NOT evaluate arbitrary dynamic expression strings

### Requirement: ReadDataByIdentifier Service Registration

The UDS protocol layer SHALL register service `0x22` ReadDataByIdentifier with the existing UDS dispatcher.

#### Scenario: 注册 `0x22` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x22` SHALL be handled by a ReadDataByIdentifier service
- **AND** unsupported service behavior for other service IDs SHALL remain unchanged

#### Scenario: DoIP 层不实现 DID 业务逻辑
- **GIVEN** a DoIP diagnostic message is received after Routing Activation
- **WHEN** the UDS payload service ID is `0x22`
- **THEN** the DoIP layer SHALL forward the payload to the UDS dispatcher
- **AND** the DoIP layer SHALL NOT parse DID configuration or encode DID values itself

### Requirement: Single DID Read Positive Response

The ReadDataByIdentifier service SHALL return configured fixed DID bytes for a single configured DID request.

#### Scenario: `22 F1 90` 返回 VIN DID 正响应
- **GIVEN** DID `0xF190` is configured with a fixed byte value
- **WHEN** the service receives UDS request bytes `0x22, 0xF1, 0x90`
- **THEN** the service SHALL return a positive response
- **AND** the encoded response SHALL start with bytes `0x62, 0xF1, 0x90`
- **AND** the response bytes after `0xF1, 0x90` SHALL equal the configured fixed DID value

#### Scenario: 正响应不改变诊断会话
- **GIVEN** the ECU runtime state has an active diagnostic session
- **WHEN** a configured DID is read successfully
- **THEN** the service SHALL NOT change the current diagnostic session
- **AND** it SHALL NOT modify SecurityAccess state

### Requirement: Multiple DID Read Ordering

The ReadDataByIdentifier service SHALL support reading multiple configured DIDs in a single request and SHALL preserve request order.

#### Scenario: 多 DID 请求按请求顺序返回
- **GIVEN** DID `0xF190` and DID `0xF191` are configured with fixed byte values
- **WHEN** the service receives UDS request bytes `0x22, 0xF1, 0x91, 0xF1, 0x90`
- **THEN** the service SHALL return a positive response with service ID `0x62`
- **AND** the response SHALL include DID `0xF191` and its value before DID `0xF190`
- **AND** the response SHALL preserve the exact DID order from the request

#### Scenario: 重复 DID 按请求重复返回
- **GIVEN** DID `0xF190` is configured with a fixed byte value
- **WHEN** the request contains DID `0xF190` more than once
- **THEN** the service SHALL encode each occurrence in request order
- **AND** it SHALL NOT collapse duplicates through dictionary uniqueness

### Requirement: DID Request Format Validation

The ReadDataByIdentifier service SHALL validate that the request contains one or more complete 2-byte DID identifiers.

#### Scenario: 空 DID 请求返回长度格式错误
- **GIVEN** the ReadDataByIdentifier service receives only service ID `0x22` with no DID bytes
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat`

#### Scenario: 奇数长度 DID 请求返回长度格式错误
- **GIVEN** the ReadDataByIdentifier service receives an odd number of DID payload bytes
- **WHEN** the service validates the request
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x13 IncorrectMessageLengthOrInvalidFormat`
- **AND** no DID read event SHALL be published for the rejected request

### Requirement: Unconfigured DID Negative Response

The ReadDataByIdentifier service SHALL reject requests for unconfigured DIDs with `0x31 RequestOutOfRange`.

#### Scenario: 未配置 DID 返回 `0x31`
- **GIVEN** DID `0xF199` is not configured
- **WHEN** the service receives UDS request bytes `0x22, 0xF1, 0x99`
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x31 RequestOutOfRange`

#### Scenario: 多 DID 中任一未配置时整体失败
- **GIVEN** DID `0xF190` is configured and DID `0xF199` is not configured
- **WHEN** the service receives a request containing both DIDs
- **THEN** it SHALL return a negative response
- **AND** the NRC SHALL be `0x31 RequestOutOfRange`
- **AND** it SHALL NOT return partial positive response data for the configured DID

### Requirement: DID Read Runtime Events

The system SHALL publish structured runtime events for successful DID reads.

#### Scenario: 单 DID 读取事件包含 ID 和响应长度
- **GIVEN** a configured DID is read successfully
- **WHEN** the service publishes the read event
- **THEN** the event data SHALL include the DID ID
- **AND** the event data SHALL include the response length for that DID value or DID response segment
- **AND** the event SHALL be visible through the existing runtime event pipeline

#### Scenario: 多 DID 读取事件覆盖每个 DID
- **GIVEN** multiple configured DIDs are read successfully in one request
- **WHEN** read events are published
- **THEN** the published event data SHALL identify each DID read
- **AND** each DID entry SHALL include the DID ID and response length
- **AND** the event data SHALL preserve or expose enough order information to verify request order

#### Scenario: 负响应不发布成功读取事件
- **GIVEN** a ReadDataByIdentifier request is rejected for invalid format or unconfigured DID
- **WHEN** the service returns a negative response
- **THEN** it SHALL NOT publish a successful DID read event
- **AND** existing UDS negative response logging behavior MAY still record the rejected response

### Requirement: DoIP Diagnostic Integration For DID Reads

The existing DoIP diagnostic forwarding path SHALL route `0x22` requests through the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后读取 DID
- **GIVEN** a TCP client has completed Routing Activation
- **AND** DID `0xF190` is configured with a fixed byte value
- **WHEN** the client sends a DoIP diagnostic message with UDS payload `0x22, 0xF1, 0x90`
- **THEN** the payload SHALL be dispatched to the ReadDataByIdentifier service
- **AND** the client SHALL receive a DoIP diagnostic response containing UDS bytes beginning with `0x62, 0xF1, 0x90`

### Requirement: Scope Boundaries
The implementation SHALL remain limited to configured static and built-in dynamic DID values for UDS `0x22` ReadDataByIdentifier.

#### Scenario: 不实现写 DID
- **GIVEN** task-013 is implemented
- **WHEN** the UDS service registry is inspected
- **THEN** this change SHALL NOT implement WriteDataByIdentifier service `0x2E`
- **AND** write DID requests SHALL continue to use existing unsupported service behavior unless another validated change defines them

#### Scenario: 不实现 ODX/PDX 导入
- **GIVEN** task-013 is implemented
- **WHEN** configuration and import code are inspected
- **THEN** it SHALL NOT add ODX import
- **AND** it SHALL NOT add PDX import
- **AND** it SHALL NOT add schema conversion from external diagnostic databases

#### Scenario: 不实现后续诊断扩展
- **GIVEN** task-013 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement DTC services
- **AND** it SHALL NOT implement Routine services
- **AND** it SHALL NOT implement Flash services
- **AND** it SHALL NOT implement SecurityAccess seed/key or unlock behavior
- **AND** it SHALL NOT add a new Web UI or Web API management surface for DID editing

### Requirement: Dynamic DID Read Positive Response
The ReadDataByIdentifier service SHALL return generated bytes for configured dynamic DID providers.

#### Scenario: `0x22` reads dynamic DID
- **GIVEN** DID `0xF192` is configured with a valid dynamic value provider
- **WHEN** the service receives UDS request bytes `0x22, 0xF1, 0x92`
- **THEN** the service SHALL return a positive response
- **AND** the encoded response SHALL start with bytes `0x62, 0xF1, 0x92`
- **AND** the response bytes after `0xF1, 0x92` SHALL equal the current generated provider value

#### Scenario: Dynamic DID read publishes normal read event
- **GIVEN** a configured dynamic DID is read successfully
- **WHEN** the service publishes the read event
- **THEN** the event data SHALL include the DID ID
- **AND** the event data SHALL include the generated response value length

### Requirement: Provider-Agnostic DID Reads
The ReadDataByIdentifier service SHALL read DID response bytes from the runtime store without interpreting DID provider configuration.

#### Scenario: Dynamic DID read uses runtime store bytes
- **GIVEN** DID `0xF192` is configured with a valid dynamic value provider
- **AND** `DidRuntimeStore` can return current generated bytes for DID `0xF192`
- **WHEN** the ReadDataByIdentifier service receives UDS request bytes `0x22, 0xF1, 0x92`
- **THEN** the service SHALL return a positive response beginning with `0x62, 0xF1, 0x92`
- **AND** the response bytes after `0xF1, 0x92` SHALL equal the bytes returned by `DidRuntimeStore`
- **AND** the service SHALL NOT inspect `valueProvider` fields directly

### Requirement: DoIP Diagnostic Integration For Dynamic DID Reads
The TCP DoIP diagnostic path SHALL expose current dynamic DID values through UDS `0x22` after Routing Activation.

#### Scenario: Routing Activation then read dynamic DID
- **GIVEN** a TCP client has completed Routing Activation
- **AND** DID `0xF192` is configured with a valid dynamic value provider
- **WHEN** the client sends a DoIP diagnostic message with UDS payload `0x22, 0xF1, 0x92`
- **THEN** the payload SHALL be dispatched to the ReadDataByIdentifier service
- **AND** the client SHALL receive a DoIP diagnostic response containing UDS bytes beginning with `0x62, 0xF1, 0x92`
- **AND** the response DID value bytes SHALL be a legal current encoded value for the configured provider

#### Scenario: DoIP dynamic DID read remains provider-agnostic
- **GIVEN** a TCP DoIP diagnostic message requests a configured dynamic DID
- **WHEN** the DoIP layer forwards the UDS payload
- **THEN** the DoIP layer SHALL NOT parse DID provider configuration
- **AND** the DoIP layer SHALL NOT calculate DID values itself

### Requirement: DID Read Event Sample Data
Successful ReadDataByIdentifier events SHALL include current DID sample data for each returned DID.

#### Scenario: Dynamic DID read event includes numeric sample
- **GIVEN** DID `0xF192` is configured with a valid dynamic numeric value provider
- **WHEN** the ReadDataByIdentifier service successfully reads DID `0xF192`
- **THEN** the published `uds.did.read` event SHALL include the DID identifier
- **AND** the event data SHALL include the raw value as uppercase hex
- **AND** the event data SHALL include the decoded numeric value
- **AND** the event data SHALL include the provider type
- **AND** the event data SHALL include the sampled timestamp
- **AND** the event data SHALL include the connection ID when available

#### Scenario: Static DID read event includes raw sample
- **GIVEN** DID `0xF190` is configured with fixed raw hex bytes and no dynamic numeric provider
- **WHEN** the ReadDataByIdentifier service successfully reads DID `0xF190`
- **THEN** the published `uds.did.read` event SHALL include the raw value as uppercase hex
- **AND** the event data SHALL NOT include a numeric value
- **AND** the provider type SHALL be `static`

#### Scenario: Rejected DID read does not publish sample event
- **GIVEN** a ReadDataByIdentifier request is rejected for invalid format, security access, or unconfigured DID
- **WHEN** the service returns a negative response
- **THEN** it SHALL NOT publish `uds.did.read` sample data for the rejected DID

