# flash-download-main-flow Specification

## Purpose
TBD - created by archiving change task-020. Update Purpose after archive.
## Requirements
### Requirement: Flash Configuration Model

The system SHALL support a Flash configuration model for the MVP download main flow without enabling real file writes or OEM flashing policies.

#### Scenario: 加载 Flash 配置
- **GIVEN** a simulator configuration contains Flash settings
- **WHEN** the configuration is loaded
- **THEN** the Flash configuration SHALL expose whether Flash download is enabled
- **AND** it SHALL expose the maximum memory size
- **AND** it SHALL expose the maximum transfer block length
- **AND** it SHALL expose the allowed diagnostic sessions
- **AND** it SHALL expose whether SecurityAccess unlock is required

#### Scenario: 拒绝无效 Flash 配置
- **GIVEN** a simulator configuration contains an invalid Flash max memory size, max block length, allowed session, or security requirement value
- **WHEN** the configuration is validated
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the invalid Flash field

#### Scenario: Flash 配置不包含真实刷写副作用
- **GIVEN** task-020 is implemented
- **WHEN** Flash configuration contracts are inspected
- **THEN** the configuration SHALL NOT require a file output path
- **AND** it SHALL NOT configure signature verification
- **AND** it SHALL NOT define a complete ECU memory map
- **AND** it SHALL NOT define OEM flashing strategy rules

### Requirement: Flash Download Runtime State

The system SHALL maintain in-memory runtime state for one active Flash download flow.

#### Scenario: 初始化下载状态
- **GIVEN** RequestDownload `0x34` is accepted
- **WHEN** the download state is initialized
- **THEN** the state SHALL record that a download is active
- **AND** it SHALL record the requested total size
- **AND** it SHALL record the accepted maximum block length
- **AND** it SHALL initialize the received byte count to zero
- **AND** it SHALL initialize the expected TransferData block sequence counter

#### Scenario: 推进下载状态
- **GIVEN** a Flash download is active
- **AND** a TransferData `0x36` request has the expected block sequence counter
- **WHEN** the transfer block is accepted
- **THEN** the state SHALL increase the received byte count by the accepted data length
- **AND** it SHALL advance the expected block sequence counter
- **AND** it SHALL keep the download active until RequestTransferExit `0x37` completes

#### Scenario: 错误请求不推进状态
- **GIVEN** a Flash download is active
- **WHEN** a TransferData `0x36` request is rejected because of an invalid block sequence counter, invalid block length, or size overflow
- **THEN** the state SHALL NOT increase the received byte count
- **AND** it SHALL NOT advance the expected block sequence counter
- **AND** it SHALL remain available for a valid recovery request according to the documented runtime convention

#### Scenario: 清理下载状态
- **GIVEN** a Flash download is active or completed
- **WHEN** the runtime clears the download state after transfer exit, cancellation, or connection disconnect
- **THEN** the state SHALL no longer report an active download
- **AND** it SHALL NOT retain payload bytes as a real file or persisted artifact

### Requirement: RequestDownload Service `0x34`

The UDS protocol layer SHALL register service `0x34` RequestDownload and implement the MVP download initialization path.

#### Scenario: 编程会话且已解锁时接受下载请求
- **GIVEN** Flash download is enabled
- **AND** the ECU is in an allowed programming diagnostic session
- **AND** the required SecurityAccess level is unlocked or security is not required
- **AND** the RequestDownload payload uses a supported format
- **AND** the requested memory size is greater than zero and does not exceed the configured maximum memory size
- **WHEN** the RequestDownload service receives request `0x34`
- **THEN** it SHALL return a positive response beginning with `0x74`
- **AND** the response SHALL communicate the accepted maximum block length
- **AND** the runtime SHALL initialize an active Flash download state

#### Scenario: 未进入编程会话时拒绝下载请求
- **GIVEN** Flash download is enabled
- **AND** the ECU is not in an allowed programming diagnostic session
- **WHEN** the RequestDownload service receives request `0x34`
- **THEN** it SHALL return a negative response for service `0x34`
- **AND** the NRC SHALL clearly indicate that the request is not allowed in the current session
- **AND** no Flash download state SHALL be initialized

#### Scenario: 未解锁时拒绝下载请求
- **GIVEN** Flash download is enabled
- **AND** Flash configuration requires SecurityAccess unlock
- **AND** the required SecurityAccess state is locked
- **WHEN** the RequestDownload service receives request `0x34`
- **THEN** it SHALL return a negative response for service `0x34`
- **AND** the NRC SHALL clearly indicate security access is denied or required
- **AND** no Flash download state SHALL be initialized

#### Scenario: 请求格式或大小无效时拒绝下载请求
- **GIVEN** the ECU is in an allowed programming diagnostic session
- **AND** the required security condition is satisfied
- **WHEN** the RequestDownload service receives an unsupported address/length format, a zero memory size, or a memory size greater than the configured maximum
- **THEN** it SHALL return a negative response for service `0x34`
- **AND** the NRC SHALL clearly indicate request format or request range failure
- **AND** no Flash download state SHALL be initialized

#### Scenario: 已有活动下载时拒绝重复下载初始化
- **GIVEN** a Flash download is already active
- **WHEN** the RequestDownload service receives another valid-looking request `0x34`
- **THEN** it SHALL return a negative response for service `0x34`
- **AND** the NRC SHALL clearly indicate a request sequence or conditions-not-correct failure
- **AND** the existing active download state SHALL NOT be replaced

### Requirement: TransferData Service `0x36`

The UDS protocol layer SHALL register service `0x36` TransferData and implement the MVP block transfer path.

#### Scenario: 接受正确块序号的数据块
- **GIVEN** a Flash download is active
- **AND** the TransferData request block sequence counter equals the expected counter
- **AND** the transfer payload length does not exceed the accepted maximum block length
- **AND** accepting the payload would not exceed the requested total download size
- **WHEN** the TransferData service receives request `0x36`
- **THEN** it SHALL return a positive response beginning with `0x76`
- **AND** the response SHALL echo the accepted block sequence counter
- **AND** the runtime SHALL update received size and next expected block sequence counter

#### Scenario: 块序号错误时拒绝数据块
- **GIVEN** a Flash download is active
- **AND** the next expected block sequence counter is `N`
- **WHEN** the TransferData service receives request `0x36` with a block sequence counter other than `N`
- **THEN** it SHALL return a negative response for service `0x36`
- **AND** the NRC SHALL clearly indicate wrong block sequence counter or equivalent request sequence failure
- **AND** the runtime SHALL NOT update received size
- **AND** the runtime SHALL NOT advance the expected block sequence counter

#### Scenario: 无活动下载时拒绝数据块
- **GIVEN** no Flash download is active
- **WHEN** the TransferData service receives request `0x36`
- **THEN** it SHALL return a negative response for service `0x36`
- **AND** the NRC SHALL clearly indicate request sequence error or conditions not correct

#### Scenario: 数据块长度超限时拒绝
- **GIVEN** a Flash download is active
- **WHEN** the TransferData service receives a transfer block larger than the accepted maximum block length or one that would exceed the requested total download size
- **THEN** it SHALL return a negative response for service `0x36`
- **AND** the NRC SHALL clearly indicate request out of range or incorrect message length
- **AND** the runtime SHALL NOT persist the payload as a real file

### Requirement: RequestTransferExit Service `0x37`

The UDS protocol layer SHALL register service `0x37` RequestTransferExit and implement the MVP download completion path.

#### Scenario: 完整接收后结束下载
- **GIVEN** a Flash download is active
- **AND** the received byte count equals the total size requested by `0x34`
- **WHEN** the RequestTransferExit service receives request `0x37`
- **THEN** it SHALL return a positive response beginning with `0x77`
- **AND** the runtime SHALL mark the download completed or clear the active download state
- **AND** it SHALL NOT write the received data to a real file

#### Scenario: 数据未完整时拒绝结束下载
- **GIVEN** a Flash download is active
- **AND** the received byte count is less than the total size requested by `0x34`
- **WHEN** the RequestTransferExit service receives request `0x37`
- **THEN** it SHALL return a negative response for service `0x37`
- **AND** the NRC SHALL clearly indicate a request sequence or conditions-not-correct failure
- **AND** the runtime SHALL NOT mark the download as successfully completed

#### Scenario: 无活动下载时拒绝结束请求
- **GIVEN** no Flash download is active
- **WHEN** the RequestTransferExit service receives request `0x37`
- **THEN** it SHALL return a negative response for service `0x37`
- **AND** the NRC SHALL clearly indicate request sequence error or conditions not correct

### Requirement: Session And Security Gating For Flash Download

The Flash download flow SHALL enforce diagnostic session and SecurityAccess gating before accepting download initialization.

#### Scenario: 会话 gating 使用已有会话状态
- **GIVEN** the ECU runtime tracks the current diagnostic session
- **WHEN** RequestDownload `0x34` is evaluated
- **THEN** the service SHALL use the existing runtime session state
- **AND** it SHALL NOT create a separate Flash-only session state

#### Scenario: 安全 gating 使用已有 SecurityAccess 状态
- **GIVEN** SecurityAccess levels are configured and runtime unlock state exists
- **WHEN** RequestDownload `0x34` is evaluated for a Flash configuration that requires security
- **THEN** the service SHALL use the existing SecurityAccess runtime unlock state
- **AND** it SHALL NOT compute seed/key values directly
- **AND** it SHALL NOT load external SecurityAccess DLLs

### Requirement: Disconnect Handling For Flash Download

The system SHALL handle connection disconnect during an active Flash download by clearing the download state or entering a documented recoverable safe state.

#### Scenario: 传输中断连后清理状态
- **GIVEN** a DoIP TCP diagnostic connection has an active Flash download
- **WHEN** the connection disconnects before RequestTransferExit completes
- **THEN** the runtime SHALL clear the active Flash download state or mark it as recoverable safe according to the documented implementation choice
- **AND** later requests SHALL NOT observe an ambiguous half-active download state

#### Scenario: 断连处理可观察
- **GIVEN** a Flash download is cancelled because of connection disconnect
- **WHEN** the runtime updates the download state
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the entry SHALL identify that the Flash download was cancelled, cleared, or made recoverable because of disconnect

### Requirement: DoIP TCP Integration For Flash Download

The existing DoIP diagnostic forwarding path SHALL route `0x34`, `0x36`, and `0x37` requests through the UDS dispatcher after Routing Activation.

#### Scenario: DoIP TCP 跑完整下载主路径
- **GIVEN** a TCP client has completed DoIP Routing Activation
- **AND** the ECU is in an allowed programming session
- **AND** the required SecurityAccess level is unlocked or not required
- **WHEN** the client sends DoIP diagnostic messages containing `0x34`, one or more valid `0x36` blocks, and `0x37`
- **THEN** the client SHALL receive DoIP diagnostic responses containing the UDS positive responses for RequestDownload, TransferData, and RequestTransferExit
- **AND** the flow SHALL complete without requiring real file output

#### Scenario: DoIP 层不实现 Flash 业务
- **GIVEN** task-020 is implemented
- **WHEN** the DoIP diagnostic message handler is inspected
- **THEN** it SHALL continue forwarding UDS payloads to the dispatcher
- **AND** it SHALL NOT parse Flash memory addresses directly
- **AND** it SHALL NOT mutate Flash download state directly except through documented connection lifecycle cleanup

### Requirement: Scope Boundaries

The implementation SHALL remain limited to the Flash download MVP main path for task-020.

#### Scenario: 不实现真实文件写入
- **GIVEN** task-020 is implemented
- **WHEN** a Flash download flow completes
- **THEN** the simulator SHALL NOT write downloaded bytes to a real file
- **AND** it SHALL NOT require a configured output file path

#### Scenario: 不实现签名验签或完整内存映射
- **GIVEN** task-020 is implemented
- **WHEN** Flash services and configuration are inspected
- **THEN** they SHALL NOT verify signatures
- **AND** they SHALL NOT calculate cryptographic digests for acceptance
- **AND** they SHALL NOT implement a complete ECU memory address map

#### Scenario: 不实现刷写后 ECU reset 联动
- **GIVEN** a Flash download completes through `0x37`
- **WHEN** the runtime handles completion
- **THEN** it SHALL NOT automatically trigger ECU reset behavior
- **AND** it SHALL NOT couple completion to service `0x11` unless a future validated change defines that behavior

#### Scenario: 不扩大到其他平台能力
- **GIVEN** task-020 is implemented
- **WHEN** changed behavior is inspected
- **THEN** it SHALL NOT add ODX/PDX import
- **AND** it SHALL NOT add PCAP support
- **AND** it SHALL NOT add TLS behavior
- **AND** it SHALL NOT add SecurityAccess DLL plugin loading
- **AND** it SHALL NOT add Web editing capability
- **AND** it SHALL NOT add a complex flashing scheduler or OEM flashing policy engine

