# Spec: TLS 传输和证书配置

**Change ID:** `task-022`
**Status:** Draft

---

## ADDED Requirements

### Requirement: TLS Configuration Contract

The system SHALL provide TLS configuration fields for enabling DoIP over TLS and configuring server and client certificates.

#### Scenario: TLS 配置字段可加载
- **GIVEN** a JSON simulator configuration contains a `tls` object
- **WHEN** the configuration is loaded
- **THEN** the system SHALL load `enabled`
- **AND** it SHALL load `serverCertificatePath`
- **AND** it SHALL load `serverCertificatePassword`
- **AND** it SHALL load `serverPrivateKeyPath`
- **AND** it SHALL load `clientCaPath`
- **AND** it SHALL load `requireClientCertificate`

#### Scenario: TLS 未启用时不要求证书
- **GIVEN** TLS configuration has `enabled` set to `false`
- **WHEN** the simulator validates configuration
- **THEN** the system SHALL NOT require server certificate paths
- **AND** it SHALL NOT require client CA configuration
- **AND** it SHALL keep TCP DoIP behavior unchanged

#### Scenario: TLS 启用时校验证书配置
- **GIVEN** TLS configuration has `enabled` set to `true`
- **WHEN** the simulator validates configuration
- **THEN** the system SHALL require enough server certificate configuration to load a TLS server certificate
- **AND** invalid or missing certificate configuration SHALL produce a clear validation error
- **AND** the error SHALL identify the TLS configuration field involved

### Requirement: TLS Listener

The system SHALL start a DoIP over TLS listener on the configured TLS port when TLS is enabled.

#### Scenario: 启动 TLS listener
- **GIVEN** TLS is enabled in simulator configuration
- **AND** a valid server certificate can be loaded
- **WHEN** runtime services are started
- **THEN** the system SHALL bind a TLS listener
- **AND** the listener SHALL use `network.doipTlsPort`
- **AND** the listener SHALL accept TLS client connection attempts
- **AND** the system SHALL publish a `tls` category event indicating the listener started

#### Scenario: TLS 端口绑定失败
- **GIVEN** TLS is enabled
- **AND** the configured DoIP TLS endpoint cannot be bound
- **WHEN** runtime services are started
- **THEN** startup SHALL fail or report a clear runtime error
- **AND** the error SHALL identify the TLS endpoint or port involved
- **AND** the system SHALL NOT silently run without TLS when TLS is configured to start

#### Scenario: TLS listener 不改变 TCP listener
- **GIVEN** both TCP DoIP and TLS DoIP are configured
- **WHEN** runtime services are started
- **THEN** the TCP listener SHALL continue to use `network.doipTcpPort`
- **AND** the TLS listener SHALL use `network.doipTlsPort`
- **AND** a failure in one accepted TLS client handshake SHALL NOT stop the TCP listener

### Requirement: Server Certificate Loading

The system SHALL load the configured server certificate for TLS handshakes.

#### Scenario: 加载合法服务端证书
- **GIVEN** TLS is enabled
- **AND** `serverCertificatePath` points to a valid server certificate
- **AND** the configured password or private key material is valid for that certificate
- **WHEN** the TLS listener starts
- **THEN** the system SHALL load a server certificate usable for TLS handshakes
- **AND** it SHALL NOT log the certificate password

#### Scenario: 服务端证书缺失
- **GIVEN** TLS is enabled
- **AND** `serverCertificatePath` is missing or points to a missing file
- **WHEN** the TLS listener starts or configuration is validated
- **THEN** the system SHALL fail TLS startup or validation with a clear error
- **AND** the error SHALL identify `serverCertificatePath`
- **AND** the system SHALL publish a `tls` category error event when runtime startup reaches certificate loading

#### Scenario: 服务端证书密码错误
- **GIVEN** TLS is enabled
- **AND** `serverCertificatePath` points to an encrypted certificate
- **AND** `serverCertificatePassword` is invalid
- **WHEN** the server certificate is loaded
- **THEN** the system SHALL reject the certificate
- **AND** the error SHALL identify certificate loading failure
- **AND** the error SHALL NOT include the password value

### Requirement: Client Certificate Validation

The system SHALL validate client certificates according to TLS configuration.

#### Scenario: 合法客户端证书建立 TLS 连接
- **GIVEN** TLS is enabled
- **AND** `requireClientCertificate` is `true`
- **AND** `clientCaPath` identifies the trusted client CA
- **AND** the client presents a certificate signed by the trusted CA
- **WHEN** the client connects to the TLS listener
- **THEN** the TLS handshake SHALL succeed
- **AND** the connection SHALL be accepted as a DoIP over TLS connection
- **AND** the system SHALL publish a `tls` category event indicating handshake success

#### Scenario: 缺失必需客户端证书时连接失败
- **GIVEN** TLS is enabled
- **AND** `requireClientCertificate` is `true`
- **WHEN** a client connects without presenting a client certificate
- **THEN** the TLS handshake or connection admission SHALL fail
- **AND** the system SHALL publish a `tls` category error event
- **AND** the event SHALL indicate that a client certificate was required but not accepted

#### Scenario: 非法客户端证书时连接失败
- **GIVEN** TLS is enabled
- **AND** `requireClientCertificate` is `true`
- **AND** `clientCaPath` identifies the trusted client CA
- **WHEN** a client presents a certificate not trusted by that CA configuration
- **THEN** the TLS handshake or connection admission SHALL fail
- **AND** the system SHALL publish a `tls` category error event
- **AND** the event SHALL include a validation failure reason without dumping private key material

#### Scenario: 不要求客户端证书时允许单向 TLS
- **GIVEN** TLS is enabled
- **AND** `requireClientCertificate` is `false`
- **WHEN** a TLS client connects without a client certificate
- **THEN** the TLS handshake MAY succeed using server authentication only
- **AND** the system SHALL NOT require `clientCaPath` only for this mode

### Requirement: TLS DoIP Processing

The system SHALL process DoIP over TLS traffic through the existing DoIP Routing Activation and UDS dispatcher path after TLS handshake succeeds.

#### Scenario: TLS 下完成 Routing Activation
- **GIVEN** a TLS client has completed TLS handshake
- **AND** the client sends a valid DoIP Routing Activation Request with an allowed tester source address
- **WHEN** the TLS DoIP server processes the frame
- **THEN** the system SHALL send a Routing Activation Response on the same TLS connection
- **AND** the response SHALL indicate activation success
- **AND** the connection SHALL be marked routing activated

#### Scenario: TLS 下完成 DiagnosticSessionControl
- **GIVEN** a TLS connection has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message containing UDS service `0x10`
- **THEN** the system SHALL dispatch the UDS payload through the existing UDS dispatcher
- **AND** the response SHALL be sent on the same TLS connection
- **AND** the behavior SHALL match the existing dispatcher semantics for `0x10`

#### Scenario: TLS 下完成 ReadDataByIdentifier
- **GIVEN** a TLS connection has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message containing UDS service `0x22`
- **THEN** the system SHALL dispatch the UDS payload through the existing UDS dispatcher
- **AND** the response SHALL be sent on the same TLS connection
- **AND** the behavior SHALL match the existing dispatcher semantics for `0x22`

#### Scenario: TLS 下完成 TesterPresent
- **GIVEN** a TLS connection has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message containing UDS service `0x3E`
- **THEN** the system SHALL dispatch the UDS payload through the existing UDS dispatcher
- **AND** the response SHALL be sent on the same TLS connection
- **AND** the behavior SHALL match the existing dispatcher semantics for `0x3E`

#### Scenario: TLS 下不实现 0x84
- **GIVEN** task-022 is implemented
- **WHEN** the implementation is inspected
- **THEN** the change SHALL NOT add UDS service `0x84`
- **AND** it SHALL NOT require `0x84` before Routing Activation or diagnostic dispatch

### Requirement: TLS Runtime Events And Logs

The system SHALL publish structured runtime events for TLS listener, connection, handshake, certificate, and error outcomes.

#### Scenario: TLS 连接事件
- **GIVEN** a TLS client connects successfully
- **WHEN** the TLS handshake completes
- **THEN** the runtime event subsystem SHALL publish a `tls` category event
- **AND** the event SHALL include the connection ID or remote endpoint
- **AND** the event SHALL indicate that the connection transport is TLS

#### Scenario: TLS 错误事件包含原因
- **GIVEN** a TLS handshake, certificate load, or client certificate validation fails
- **WHEN** the failure is handled
- **THEN** the runtime event subsystem SHALL publish a `tls` category error or warning event
- **AND** the event SHALL include a reason summary
- **AND** the event SHALL include the remote endpoint when available
- **AND** the event SHALL NOT include certificate private key material or certificate password values

#### Scenario: TLS 文件日志可见
- **GIVEN** structured file logging is configured
- **WHEN** TLS connection or error events are published
- **THEN** the file log SHALL contain those `tls` events
- **AND** the logged events SHALL preserve the reason summary and connection metadata

### Requirement: TLS Connection Visibility In Web UI

The WebApi and WebConsole SHALL distinguish TCP and TLS connections.

#### Scenario: WebApi 返回 TLS transport
- **GIVEN** a TLS DoIP connection is active
- **WHEN** the WebApi returns connection snapshots or simulator status containing active connections
- **THEN** the TLS connection SHALL include `transport` with value `tls`
- **AND** the snapshot SHALL preserve routing activation and logical address fields when available

#### Scenario: WebApi 保持 TCP transport
- **GIVEN** a TCP DoIP connection is active
- **WHEN** the WebApi returns connection snapshots or simulator status containing active connections
- **THEN** the TCP connection SHALL include `transport` with value `tcp`
- **AND** existing TCP connection behavior SHALL remain compatible

#### Scenario: WebConsole 区分 TCP 和 TLS
- **GIVEN** the WebConsole displays connection status
- **WHEN** the connection list contains both TCP and TLS connections
- **THEN** the UI SHALL visually or textually distinguish TCP from TLS
- **AND** the UI SHALL NOT add certificate generation controls
- **AND** the UI SHALL NOT add packet decryption, packet search, or pcapng metadata controls

### Requirement: TLS PCAP Boundary

The task-022 implementation SHALL NOT require decrypting TLS contents in PCAP output.

#### Scenario: TLS 流量录制为加密网络字节
- **GIVEN** PCAP recording is active
- **AND** a TLS DoIP client exchanges traffic with the simulator
- **WHEN** packets are recorded
- **THEN** the recorder MAY record TLS network bytes
- **AND** the system SHALL NOT default to decrypting TLS payloads for PCAP output
- **AND** the implementation SHALL NOT add TLS key logging for this task

### Requirement: TLS Scope Boundaries

The task-022 implementation SHALL remain limited to TLS transport and certificate configuration.

#### Scenario: 不实现证书生成 UI
- **GIVEN** task-022 is implemented
- **WHEN** the Web UI is inspected
- **THEN** the change SHALL NOT add certificate generation UI
- **AND** it SHALL NOT add certificate authority management UI

#### Scenario: 不扩大诊断和安全插件范围
- **GIVEN** task-022 is implemented
- **WHEN** the implementation is inspected
- **THEN** the change SHALL NOT add ODX or PDX import behavior
- **AND** it SHALL NOT add SecurityAccess DLL plugin behavior
- **AND** it SHALL NOT add fault or exception injection behavior

#### Scenario: 不增加高级抓包能力
- **GIVEN** task-022 is implemented
- **WHEN** the implementation is inspected
- **THEN** the change SHALL NOT add pcapng advanced metadata
- **AND** it SHALL NOT add packet index search
- **AND** it SHALL NOT add default TLS payload decryption for PCAP

### Requirement: TLS Verification

The task-022 implementation SHALL include focused verification for TLS transport and certificate behavior.

#### Scenario: 合法 mTLS 集成测试
- **GIVEN** automated tests create or provide a trusted server certificate and trusted client certificate
- **WHEN** a TLS client connects with the trusted client certificate
- **THEN** the test SHALL verify the TLS connection succeeds
- **AND** it SHALL verify Routing Activation succeeds on that TLS connection

#### Scenario: TLS UDS 主路径测试
- **GIVEN** a TLS connection has completed Routing Activation in an automated test
- **WHEN** the test sends UDS `0x10`, `0x22`, and `0x3E` over DoIP diagnostic messages
- **THEN** each request SHALL receive the expected response from the existing UDS dispatcher path

#### Scenario: 非法证书测试
- **GIVEN** TLS requires client certificates in an automated test
- **WHEN** the client omits a certificate or presents an untrusted certificate
- **THEN** the test SHALL verify connection failure
- **AND** it SHALL verify a log or runtime event includes an error reason

#### Scenario: Web transport 区分测试
- **GIVEN** connection snapshots include TCP and TLS connections
- **WHEN** WebApi or WebConsole tests render or inspect those connections
- **THEN** the tests SHALL verify TCP connections are identified as `tcp`
- **AND** TLS connections are identified as `tls`

## MODIFIED Requirements

None.

## REMOVED Requirements

None.

