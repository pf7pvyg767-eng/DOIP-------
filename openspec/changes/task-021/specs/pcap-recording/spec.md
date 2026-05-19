# Spec: PCAP 录制 MVP

**Change ID:** `task-021`
**Status:** Draft

---

## ADDED Requirements

### Requirement: PCAP Writer

The system SHALL provide a pcap writer that creates standard `.pcap` files readable by Wireshark.

#### Scenario: 创建 pcap global header
- **GIVEN** recording is started for a new pcap file
- **WHEN** the pcap writer creates the file
- **THEN** the file SHALL begin with a valid pcap global header
- **AND** the header SHALL declare a Wireshark-readable link type
- **AND** the writer SHALL NOT create pcapng-only sections or pcapng metadata blocks

#### Scenario: 写入 packet record header
- **GIVEN** the pcap writer has an open file
- **WHEN** a UDP or TCP DoIP packet is recorded
- **THEN** the writer SHALL append a pcap packet record header
- **AND** the record header SHALL include a timestamp
- **AND** the record header SHALL include captured length and original length
- **AND** the record SHALL include the bytes required for Wireshark to identify the recorded UDP or TCP traffic

#### Scenario: 关闭 pcap writer
- **GIVEN** a pcap writer has an open file
- **WHEN** recording is stopped
- **THEN** the writer SHALL flush pending data
- **AND** it SHALL close the file handle
- **AND** the file SHALL remain readable as a pcap file

### Requirement: PCAP Recorder Lifecycle

The system SHALL support starting, stopping, and querying a single active PCAP recording session.

#### Scenario: 开始录制
- **GIVEN** no pcap recording is active
- **WHEN** a caller starts pcap recording
- **THEN** the recorder SHALL create a `.pcap` file under the configured or default pcap output directory
- **AND** the recorder SHALL report `recording` as `true`
- **AND** the recorder SHALL report the created `filePath`
- **AND** the recorder SHALL initialize `bytesWritten`
- **AND** the recorder SHALL report `maxBytes` as `524288000`
- **AND** the system SHALL publish a `pcap` category runtime event for recording start

#### Scenario: 停止录制
- **GIVEN** pcap recording is active
- **WHEN** a caller stops pcap recording
- **THEN** the recorder SHALL stop accepting new packet records for that session
- **AND** it SHALL flush and close the active pcap file
- **AND** it SHALL report `recording` as `false`
- **AND** the system SHALL publish a `pcap` category runtime event for recording stop

#### Scenario: 查询录制状态
- **GIVEN** the simulator runtime is available
- **WHEN** a caller queries pcap recording status
- **THEN** the status SHALL include `recording`
- **AND** the status SHALL include `filePath` when a recording file has been created
- **AND** the status SHALL include `bytesWritten`
- **AND** the status SHALL include `maxBytes`
- **AND** the status query SHALL NOT start or stop recording

#### Scenario: 重复开始录制
- **GIVEN** pcap recording is already active
- **WHEN** a caller starts pcap recording again
- **THEN** the system SHALL NOT corrupt the active pcap file
- **AND** it SHALL return the current active recording status or a clear conflict result according to existing WebApi conventions
- **AND** it SHALL NOT start a second concurrent recording session

### Requirement: UDP DoIP Recording

The system SHALL record UDP DoIP discovery send and receive data while pcap recording is active.

#### Scenario: 记录 UDP discovery 接收
- **GIVEN** pcap recording is active
- **AND** a UDP DoIP discovery packet is received
- **WHEN** the UDP transport passes the packet through the simulator
- **THEN** the recorder SHALL receive a packet record request
- **AND** the record SHALL identify the packet as inbound
- **AND** the record SHALL include timestamp, local endpoint, remote endpoint, transport type, and payload bytes
- **AND** the UDP discovery behavior SHALL remain unchanged

#### Scenario: 记录 UDP discovery 发送
- **GIVEN** pcap recording is active
- **AND** the simulator sends a UDP DoIP discovery response
- **WHEN** the UDP transport sends the packet
- **THEN** the recorder SHALL receive a packet record request
- **AND** the record SHALL identify the packet as outbound
- **AND** the record SHALL include timestamp, local endpoint, remote endpoint, transport type, and payload bytes
- **AND** the UDP response behavior SHALL remain unchanged

#### Scenario: 未录制时不写 UDP 数据
- **GIVEN** pcap recording is not active
- **WHEN** UDP DoIP discovery packets are received or sent
- **THEN** the recorder SHALL NOT create a pcap packet record
- **AND** the UDP discovery behavior SHALL remain unchanged

### Requirement: TCP DoIP And UDS Recording

The system SHALL record TCP DoIP and UDS channel send and receive data while pcap recording is active.

#### Scenario: 记录 TCP 接收数据
- **GIVEN** pcap recording is active
- **AND** a TCP client sends DoIP data that may contain UDS payload
- **WHEN** the TCP transport receives the bytes
- **THEN** the recorder SHALL receive a packet record request
- **AND** the record SHALL identify the packet as inbound
- **AND** the record SHALL include timestamp, connection or endpoint metadata when available, transport type, and payload bytes
- **AND** Routing Activation and UDS dispatch behavior SHALL remain unchanged

#### Scenario: 记录 TCP 发送数据
- **GIVEN** pcap recording is active
- **AND** the simulator sends DoIP data over TCP
- **WHEN** the TCP transport writes the bytes to the client
- **THEN** the recorder SHALL receive a packet record request
- **AND** the record SHALL identify the packet as outbound
- **AND** the record SHALL include timestamp, connection or endpoint metadata when available, transport type, and payload bytes
- **AND** DoIP response behavior SHALL remain unchanged

#### Scenario: TCP 断开后不破坏录制状态
- **GIVEN** pcap recording is active
- **WHEN** a TCP client disconnects
- **THEN** the recorder SHALL keep the recording session usable for later packets until stop or size limit
- **AND** it SHALL NOT close the pcap file only because one TCP connection closed

### Requirement: PCAP Size Limit

The system SHALL enforce a default pcap file size limit of 500MiB.

#### Scenario: 写入前检查大小上限
- **GIVEN** pcap recording is active
- **AND** the active file is below `524288000` bytes
- **WHEN** a packet record would make the file exceed `524288000` bytes
- **THEN** the recorder SHALL NOT append that packet record
- **AND** it SHALL stop the active recording session
- **AND** it SHALL publish a `pcap` category runtime event indicating the size limit was reached
- **AND** the status SHALL report `recording` as `false`

#### Scenario: 大小上限未达到时继续录制
- **GIVEN** pcap recording is active
- **AND** appending a packet record keeps the file size at or below `524288000` bytes
- **WHEN** the packet is recorded
- **THEN** the recorder SHALL append the packet record
- **AND** it SHALL update `bytesWritten`
- **AND** recording SHALL remain active

#### Scenario: 上限到达后不自动轮转
- **GIVEN** pcap recording has stopped because the size limit was reached
- **WHEN** more UDP or TCP packets pass through the simulator
- **THEN** the recorder SHALL NOT automatically create a second pcap file for this task
- **AND** the system SHALL require a new explicit start request before another recording session begins

### Requirement: PCAP Web API

The WebApi SHALL expose endpoints for pcap recording status, start, and stop.

#### Scenario: 查询状态 API
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `GET /api/pcap/status`
- **THEN** the response SHALL be HTTP 200
- **AND** the response body SHALL include `recording`, `filePath`, `bytesWritten`, and `maxBytes`
- **AND** the call SHALL NOT mutate recording state

#### Scenario: 开始录制 API
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `POST /api/pcap/start`
- **THEN** the API SHALL attempt to start pcap recording
- **AND** the response SHALL include the current pcap recording status
- **AND** successful start SHALL create a pcap file

#### Scenario: 停止录制 API
- **GIVEN** the WebApi is running
- **WHEN** a caller sends `POST /api/pcap/stop`
- **THEN** the API SHALL attempt to stop pcap recording
- **AND** the response SHALL include the current pcap recording status
- **AND** successful stop SHALL close the pcap file

### Requirement: PCAP Web Status Display

The WebConsole SHALL display the current pcap recording status.

#### Scenario: 显示录制状态
- **GIVEN** the WebConsole is open
- **WHEN** the pcap status API reports the current recorder state
- **THEN** the UI SHALL show whether recording is active
- **AND** it SHALL show the current file path when available
- **AND** it SHALL show bytes written and max bytes

#### Scenario: 开始和停止操作
- **GIVEN** the WebConsole pcap status view is available
- **WHEN** the user starts or stops recording from the UI
- **THEN** the UI SHALL call the corresponding WebApi endpoint
- **AND** it SHALL refresh or update the displayed status from the API response
- **AND** it SHALL NOT add pcap download, packet search, packet replay, or chart analysis controls

#### Scenario: 上限事件更新状态
- **GIVEN** the WebConsole receives a runtime event indicating pcap size limit reached
- **WHEN** the pcap status view is active
- **THEN** the UI SHALL show recording as stopped after refreshing or applying the event
- **AND** it SHALL keep the last file path and byte count visible when available

### Requirement: PCAP Scope Boundaries

The task-021 implementation SHALL remain limited to PCAP recording MVP behavior.

#### Scenario: 不保证 TLS 内容解密
- **GIVEN** traffic is protected by TLS in a future or existing configuration
- **WHEN** pcap recording is active
- **THEN** the recorder MAY record encrypted bytes as network traffic
- **AND** it SHALL NOT guarantee decrypted TLS payload visibility
- **AND** it SHALL NOT add TLS key logging or TLS transport behavior for this task

#### Scenario: 不实现高级分析能力
- **GIVEN** task-021 is implemented
- **WHEN** the codebase is inspected
- **THEN** the change SHALL NOT add pcapng advanced metadata
- **AND** it SHALL NOT add packet index search
- **AND** it SHALL NOT add packet replay
- **AND** it SHALL NOT add pcap download endpoints unless explicitly required by a later task

#### Scenario: 不扩大诊断功能范围
- **GIVEN** task-021 is implemented
- **WHEN** the implementation scope is reviewed
- **THEN** the change SHALL NOT add ODX/PDX import
- **AND** it SHALL NOT add SecurityAccess plugin behavior
- **AND** it SHALL NOT add fault injection behavior
- **AND** it SHALL NOT change UDS service semantics beyond observing existing TCP payload bytes

### Requirement: PCAP Verification

The task-021 implementation SHALL include focused automated verification for the recording MVP.

#### Scenario: writer 单元测试
- **GIVEN** the pcap writer is tested
- **WHEN** a test writes one or more packet records
- **THEN** the test SHALL verify the pcap global header
- **AND** it SHALL verify packet record header fields
- **AND** it SHALL verify non-empty packet bytes are written

#### Scenario: UDP/TCP 集成测试
- **GIVEN** pcap recording is started in an integration test
- **WHEN** the test performs UDP discovery and a TCP UDS request
- **THEN** the generated pcap file SHALL exist
- **AND** the file size SHALL be greater than the pcap global header size
- **AND** the file SHALL contain at least one recorded packet from UDP or TCP traffic

#### Scenario: Wireshark 可打开验证
- **GIVEN** a pcap file is generated by the simulator
- **WHEN** the file is opened with Wireshark or validated by an equivalent pcap parser in automated tests
- **THEN** the file SHALL be recognized as a pcap file
- **AND** the validation result SHALL be documented in test output or manual verification notes
