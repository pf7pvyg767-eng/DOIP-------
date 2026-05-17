# Spec: DoIP 帧编解码核心

**Change ID:** `task-008`
**Status:** Draft

---

## ADDED Requirements

### Requirement: DoIP Payload Type Contract

The DoIP protocol layer SHALL define a payload type contract that represents known DoIP payload types while preserving unknown raw payload type values.

#### Scenario: 定义已知 payload type
- **GIVEN** the DoIP protocol layer is available
- **WHEN** payload types are referenced by codec or future payload parsers
- **THEN** the system SHALL provide named constants, enum values, or an equivalent typed contract for known DoIP payload types
- **AND** the contract SHALL expose the underlying `ushort` payload type value

#### Scenario: 保留未知 payload type
- **GIVEN** a DoIP frame contains a payload type value that is not defined as a known payload type
- **WHEN** the codec decodes the frame
- **THEN** the decoded frame SHALL preserve the original `ushort` payload type value
- **AND** the decoded result SHALL allow callers to determine that the payload type is unknown
- **AND** the codec SHALL NOT fail only because the payload type is unknown

### Requirement: DoIP Frame And Header Contracts

The DoIP protocol layer SHALL provide reusable header and frame contracts for fixed-header parsing and later payload parsing.

#### Scenario: 定义 header 基础类型
- **GIVEN** later payload parsing needs access to DoIP header fields
- **WHEN** the protocol contracts are defined
- **THEN** the system SHALL provide a `DoipHeader` or equivalent type
- **AND** the header type SHALL include protocol version
- **AND** the header type SHALL include inverse protocol version
- **AND** the header type SHALL include payload type raw value
- **AND** the header type SHALL include payload length

#### Scenario: 定义 frame 基础类型
- **GIVEN** a complete DoIP message has a header and payload bytes
- **WHEN** the protocol contracts are defined
- **THEN** the system SHALL provide a `DoipFrame` or equivalent type
- **AND** the frame type SHALL include reusable header information
- **AND** the frame type SHALL include payload bytes without requiring payload-specific parsing

### Requirement: DoIP Header Decoding

The DoIP codec SHALL decode the fixed 8-byte DoIP header from an in-memory byte sequence.

#### Scenario: 解码合法 header
- **GIVEN** an input byte sequence contains at least 8 bytes of DoIP header data
- **WHEN** the codec decodes the header
- **THEN** it SHALL read protocol version from byte 0
- **AND** it SHALL read inverse protocol version from byte 1
- **AND** it SHALL read payload type from bytes 2 through 3 using network byte order
- **AND** it SHALL read payload length from bytes 4 through 7 using network byte order

#### Scenario: header 长度不足
- **GIVEN** an input byte sequence contains fewer than 8 bytes
- **WHEN** the codec decodes the header or frame
- **THEN** it SHALL return a failed result with a header-too-short error
- **AND** the error SHALL identify the expected 8-byte minimum header length
- **AND** the codec SHALL NOT read beyond the available input bytes

### Requirement: Protocol Version Validation

The DoIP codec SHALL validate protocol version fields before accepting a decoded frame.

#### Scenario: 支持的 protocol version
- **GIVEN** a DoIP frame uses a supported protocol version such as `0x02`
- **WHEN** the codec decodes the frame
- **THEN** protocol version validation SHALL pass
- **AND** subsequent inverse version and payload length validation SHALL be performed

#### Scenario: 不支持的 protocol version
- **GIVEN** a DoIP frame uses a protocol version that the implementation does not support
- **WHEN** the codec decodes the frame
- **THEN** it SHALL return a failed result with an unsupported-protocol-version error
- **AND** the error SHALL include or expose the received protocol version

#### Scenario: inverse version 不匹配
- **GIVEN** a DoIP frame contains a protocol version and inverse protocol version that are not bitwise complements
- **WHEN** the codec decodes the frame
- **THEN** it SHALL return a failed result with an inverse-version-mismatch error
- **AND** the error SHALL include or expose the received protocol version and inverse protocol version

### Requirement: Payload Length Validation

The DoIP codec SHALL validate that the declared payload length matches the actual number of payload bytes supplied to the frame decoder.

#### Scenario: payload length 与实际长度一致
- **GIVEN** a DoIP frame header declares payload length `N`
- **AND** the input contains exactly `N` bytes after the 8-byte header
- **WHEN** the codec decodes the frame
- **THEN** payload length validation SHALL pass
- **AND** the decoded frame SHALL contain exactly those `N` payload bytes

#### Scenario: payload length 小于实际长度
- **GIVEN** a DoIP frame header declares a payload length smaller than the number of bytes after the 8-byte header
- **WHEN** the codec decodes the frame
- **THEN** it SHALL return a failed result with a payload-length-mismatch error
- **AND** the error SHALL include or expose both the declared length and the actual payload byte count

#### Scenario: payload length 大于实际长度
- **GIVEN** a DoIP frame header declares a payload length greater than the number of bytes after the 8-byte header
- **WHEN** the codec decodes the frame
- **THEN** it SHALL return a failed result with a payload-length-mismatch error
- **AND** the error SHALL include or expose both the declared length and the actual payload byte count

### Requirement: DoIP Frame Encoding

The DoIP codec SHALL encode a DoIP frame into an in-memory byte sequence using the fixed header format and network byte order.

#### Scenario: 编码合法 frame
- **GIVEN** a DoIP frame has a supported protocol version, a payload type raw value, and payload bytes
- **WHEN** the codec encodes the frame
- **THEN** the output SHALL contain protocol version at byte 0
- **AND** the output SHALL contain inverse protocol version at byte 1
- **AND** the output SHALL contain payload type in bytes 2 through 3 using network byte order
- **AND** the output SHALL contain payload length in bytes 4 through 7 using network byte order
- **AND** the output SHALL append the payload bytes after the 8-byte header

#### Scenario: 合法 frame round-trip
- **GIVEN** a valid DoIP frame contains a known payload type and payload bytes
- **WHEN** the frame is encoded and then decoded
- **THEN** the decoded frame SHALL preserve protocol version
- **AND** the decoded frame SHALL preserve payload type raw value
- **AND** the decoded frame SHALL preserve payload bytes
- **AND** the decoded frame SHALL have payload length equal to the payload byte count

#### Scenario: 编码处理 payload length 不一致
- **GIVEN** an input frame representation contains payload length metadata that does not match the payload byte count
- **WHEN** the codec encodes the frame
- **THEN** the codec SHALL either calculate payload length from the payload bytes or return a failed result with an invalid-encode-input error
- **AND** the selected behavior SHALL be documented by the implementation
- **AND** the codec SHALL NOT silently emit a frame whose declared payload length disagrees with its payload byte count

### Requirement: Codec Error Model

The DoIP codec SHALL provide an explicit error model for normal protocol validation failures.

#### Scenario: 返回可断言错误
- **GIVEN** codec input fails a protocol validation rule
- **WHEN** the codec returns the result
- **THEN** the failed result SHALL expose a machine-testable error code or error type
- **AND** the failed result SHALL expose a human-readable message or equivalent diagnostic detail
- **AND** tests SHALL NOT need to parse exception text to determine the error kind

#### Scenario: 关键错误类型可区分
- **GIVEN** invalid inputs cover multiple failure reasons
- **WHEN** the codec reports errors
- **THEN** header-too-short SHALL be distinguishable from unsupported-protocol-version
- **AND** unsupported-protocol-version SHALL be distinguishable from inverse-version-mismatch
- **AND** inverse-version-mismatch SHALL be distinguishable from payload-length-mismatch
- **AND** payload-length-mismatch SHALL be distinguishable from invalid-encode-input when encoding validation is used

### Requirement: Codec Independence From Network Runtime

The DoIP codec SHALL be independently testable without sockets or running network services.

#### Scenario: codec 不依赖 socket
- **GIVEN** the DoIP codec is instantiated in a unit test
- **WHEN** tests encode or decode DoIP frames
- **THEN** the tests SHALL operate only on in-memory byte sequences
- **AND** the codec SHALL NOT require UDP sockets
- **AND** the codec SHALL NOT require TCP sockets
- **AND** the codec SHALL NOT require the Host or WebApi to be running

#### Scenario: 不扩展业务协议运行时
- **GIVEN** task-008 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT implement routing activation business behavior
- **AND** it SHALL NOT implement UDS services
- **AND** it SHALL NOT extend Web UI behavior
- **AND** it SHALL NOT extend event stream behavior

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
