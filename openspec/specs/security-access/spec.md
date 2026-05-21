# security-access Specification

## Purpose
TBD - created by archiving change task-018. Update Purpose after archive.
## Requirements
### Requirement: SecurityAccess Configuration Model

The system SHALL support a SecurityAccess configuration model for seed/key MVP behavior using built-in algorithms only.

#### Scenario: 加载安全等级配置
- **GIVEN** a simulator configuration contains a SecurityAccess level entry
- **WHEN** the configuration is loaded
- **THEN** the entry SHALL expose the configured security level
- **AND** it SHALL expose the seed request sub-function
- **AND** it SHALL expose the key send sub-function
- **AND** it SHALL expose the algorithm type and parameter
- **AND** it SHALL expose `maxFailedAttempts` and `lockoutMs`

#### Scenario: 拒绝重复安全等级或子功能
- **GIVEN** a simulator configuration contains duplicate SecurityAccess levels or duplicate seed/key sub-functions
- **WHEN** the configuration is validated
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the conflicting SecurityAccess field

#### Scenario: 拒绝无效安全访问配置
- **GIVEN** a simulator configuration contains an invalid SecurityAccess level, algorithm type, algorithm parameter, failed attempt limit, or lockout value
- **WHEN** the configuration is validated
- **THEN** validation SHALL fail
- **AND** the validation result SHALL identify the invalid SecurityAccess field

#### Scenario: 不加载外部安全算法
- **GIVEN** task-018 is implemented
- **WHEN** SecurityAccess configuration is inspected
- **THEN** the configuration SHALL only select built-in algorithm identifiers
- **AND** it SHALL NOT load a DLL
- **AND** it SHALL NOT reference an OEM external algorithm package

### Requirement: Built-In Seed And Key Algorithm

The system SHALL provide at least one deterministic built-in example algorithm for computing expected keys from generated seeds.

#### Scenario: 使用内置 XOR 或加法算法计算 key
- **GIVEN** a SecurityAccess level is configured with a supported built-in algorithm and parameter
- **AND** the runtime has generated a seed for that level
- **WHEN** the expected key is computed
- **THEN** the algorithm SHALL produce a deterministic expected key from the seed and parameter
- **AND** automated tests SHALL be able to verify the exact expected key
- **AND** the algorithm SHALL NOT claim OEM or cryptographic security strength

#### Scenario: 拒绝未知内置算法类型
- **GIVEN** a SecurityAccess level references an unknown algorithm type
- **WHEN** the SecurityAccess service or configuration validator resolves the algorithm
- **THEN** the operation SHALL fail with a clear error
- **AND** the simulator SHALL NOT silently fall back to a different algorithm

### Requirement: SecurityAccess Runtime State

The system SHALL maintain in-memory runtime state for each configured SecurityAccess level.

#### Scenario: 初始化安全等级状态
- **GIVEN** the simulator runtime starts with configured SecurityAccess levels
- **WHEN** the ECU runtime state is created
- **THEN** each configured security level SHALL start locked
- **AND** each level SHALL expose failed attempt count as zero
- **AND** each level SHALL expose no active lockout
- **AND** the state SHALL NOT be persisted to configuration files

#### Scenario: 保存最近 seed
- **GIVEN** a tester requests a seed for a configured security level
- **WHEN** the SecurityAccess service accepts the seed request
- **THEN** the runtime state SHALL store the generated seed for that level
- **AND** the stored seed SHALL be used to validate the following key request for the same level

#### Scenario: 解锁指定安全等级
- **GIVEN** a configured security level has an active generated seed
- **WHEN** the tester sends the correct key for the matching key sub-function
- **THEN** the runtime state SHALL mark that specific security level as unlocked
- **AND** it SHALL reset failed attempt count for that level
- **AND** it SHALL NOT unlock unrelated security levels

### Requirement: SecurityAccess Service `0x27`

The UDS protocol layer SHALL register service `0x27` SecurityAccess and implement the request seed / send key main path.

#### Scenario: 注册 `0x27` 服务
- **GIVEN** the Host configures the UDS dispatcher
- **WHEN** UDS services are registered
- **THEN** service ID `0x27` SHALL be handled by a SecurityAccess service
- **AND** unrelated service behavior SHALL remain unchanged

#### Scenario: 请求 seed 返回非空 seed
- **GIVEN** SecurityAccess level `1` is configured with seed request sub-function `0x01`
- **WHEN** the SecurityAccess service receives request bytes `0x27, 0x01`
- **THEN** it SHALL return a positive `0x67` response
- **AND** the response SHALL echo sub-function `0x01`
- **AND** the response SHALL contain a non-empty seed

#### Scenario: 正确 key 解锁安全等级
- **GIVEN** SecurityAccess level `1` has returned a non-empty seed for sub-function `0x01`
- **AND** the tester computes the correct key using the configured built-in algorithm
- **WHEN** the SecurityAccess service receives request bytes beginning with `0x27, 0x02` and the correct key bytes
- **THEN** it SHALL return a positive `0x67` response
- **AND** the response SHALL echo sub-function `0x02`
- **AND** SecurityAccess level `1` SHALL become unlocked

#### Scenario: 未知子功能返回明确 NRC
- **GIVEN** the SecurityAccess service receives a `0x27` request for an unconfigured sub-function
- **WHEN** the request is validated
- **THEN** it SHALL return a negative response for service `0x27`
- **AND** the NRC SHALL clearly indicate unsupported sub-function or request out of range
- **AND** no security level SHALL become unlocked

#### Scenario: key 请求前没有 seed 时返回 NRC
- **GIVEN** SecurityAccess level `1` has not issued a seed in the current runtime state
- **WHEN** the SecurityAccess service receives the key send sub-function for level `1`
- **THEN** it SHALL return a negative response for service `0x27`
- **AND** the NRC SHALL clearly indicate request sequence error or equivalent project convention
- **AND** the failed attempt count SHALL NOT be used to bypass seed request ordering

### Requirement: Failed Key Attempts And Lockout

The system SHALL count failed key attempts and lock a security level after the configured threshold.

#### Scenario: 错误 key 返回 NRC 并累计失败次数
- **GIVEN** SecurityAccess level `1` has returned a seed
- **AND** the failed attempt count for level `1` is below `maxFailedAttempts`
- **WHEN** the tester sends an incorrect key for level `1`
- **THEN** the service SHALL return a negative response for service `0x27`
- **AND** the NRC SHALL clearly indicate invalid key or equivalent project convention
- **AND** the failed attempt count for level `1` SHALL increase by one
- **AND** level `1` SHALL remain locked

#### Scenario: 达到失败次数后进入锁定状态
- **GIVEN** SecurityAccess level `1` has `maxFailedAttempts` configured as `3`
- **AND** the tester has sent incorrect keys until the failed attempt count reaches `3`
- **WHEN** the third failed key is processed
- **THEN** level `1` SHALL enter lockout state
- **AND** the lockout state SHALL remain active until the configured `lockoutMs` duration has elapsed
- **AND** level `1` SHALL remain locked

#### Scenario: 锁定期间拒绝 seed 或 key 请求
- **GIVEN** SecurityAccess level `1` is in lockout state
- **WHEN** the SecurityAccess service receives a seed request or key request for level `1`
- **THEN** it SHALL return a negative response for service `0x27`
- **AND** the NRC SHALL clearly indicate that the required delay has not expired or equivalent project convention
- **AND** it SHALL NOT reset the lockout by accepting the request

#### Scenario: 锁定时间结束后允许重新请求 seed
- **GIVEN** SecurityAccess level `1` is in lockout state
- **AND** the configured lockout duration has elapsed
- **WHEN** the tester requests a seed for level `1`
- **THEN** the service SHALL accept the seed request
- **AND** it SHALL return a non-empty seed
- **AND** the failed attempt count for level `1` SHALL be reset or made ready for a new attempt sequence according to the documented runtime convention

### Requirement: Protected DID Access

The ReadDataByIdentifier service SHALL enforce configured SecurityAccess level requirements for protected DIDs.

#### Scenario: 未解锁时读取受保护 DID 失败
- **GIVEN** DID `0xF190` is configured with a required SecurityAccess level
- **AND** that security level is locked
- **WHEN** the ReadDataByIdentifier service receives a request for DID `0xF190`
- **THEN** it SHALL return a negative response for service `0x22`
- **AND** the NRC SHALL clearly indicate security access denied or equivalent project convention
- **AND** it SHALL NOT return the protected DID value

#### Scenario: 解锁后读取受保护 DID 成功
- **GIVEN** DID `0xF190` is configured with a required SecurityAccess level
- **AND** that security level has been unlocked through `0x27`
- **WHEN** the ReadDataByIdentifier service receives a request for DID `0xF190`
- **THEN** it SHALL return the existing positive DID response
- **AND** the response SHALL include the configured DID value

#### Scenario: 未受保护 DID 不受安全状态影响
- **GIVEN** DID `0xF191` is configured without a required SecurityAccess level
- **AND** all SecurityAccess levels are locked
- **WHEN** the ReadDataByIdentifier service receives a request for DID `0xF191`
- **THEN** it SHALL follow the existing DID read behavior
- **AND** it SHALL NOT require SecurityAccess unlock for that DID

### Requirement: Protected Routine Access

The RoutineControl service SHALL enforce configured SecurityAccess level requirements for protected Routines.

#### Scenario: 未解锁时调用受保护 Routine 失败
- **GIVEN** Routine `0x0201` is configured with a required SecurityAccess level
- **AND** that security level is locked
- **WHEN** the RoutineControl service receives a valid request for Routine `0x0201`
- **THEN** it SHALL return a negative response for service `0x31`
- **AND** the NRC SHALL clearly indicate security access denied or equivalent project convention
- **AND** it SHALL NOT return the configured fixed success payload

#### Scenario: 解锁后调用受保护 Routine 成功
- **GIVEN** Routine `0x0201` is configured with a required SecurityAccess level
- **AND** that security level has been unlocked through `0x27`
- **WHEN** the RoutineControl service receives a valid request for Routine `0x0201`
- **THEN** it SHALL return the existing positive RoutineControl response
- **AND** the response SHALL include the configured fixed response payload

#### Scenario: 未受保护 Routine 不受安全状态影响
- **GIVEN** Routine `0x0202` is configured without a required SecurityAccess level
- **AND** all SecurityAccess levels are locked
- **WHEN** the RoutineControl service receives a valid request for Routine `0x0202`
- **THEN** it SHALL follow the existing RoutineControl behavior
- **AND** it SHALL NOT require SecurityAccess unlock for that Routine

### Requirement: SecurityAccess Events And Logs

The system SHALL publish runtime events or structured logs for SecurityAccess state changes and rejected security attempts.

#### Scenario: seed 请求产生安全事件
- **GIVEN** a configured SecurityAccess seed request is accepted
- **WHEN** the seed response is generated
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL identify service `0x27`
- **AND** it SHALL include the security level and operation outcome

#### Scenario: 解锁成功产生安全事件
- **GIVEN** a correct key unlocks a configured security level
- **WHEN** the runtime state is updated
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL identify the unlocked security level

#### Scenario: 错误 key 或锁定产生安全事件
- **GIVEN** an incorrect key is rejected or a security level enters lockout
- **WHEN** the negative response is returned
- **THEN** the system SHALL publish a runtime event or structured log entry
- **AND** the event SHALL include the security level and rejection reason
- **AND** it SHALL NOT log secret key material in clear text

### Requirement: DoIP Diagnostic Integration For SecurityAccess

The existing DoIP diagnostic forwarding path SHALL route `0x27` requests through the UDS dispatcher after Routing Activation.

#### Scenario: Routing Activation 后请求 seed
- **GIVEN** a TCP client has completed Routing Activation
- **WHEN** the client sends a DoIP diagnostic message with UDS payload `0x27, 0x01`
- **THEN** the payload SHALL be dispatched to the SecurityAccess service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x67` seed response

#### Scenario: Routing Activation 后发送 key
- **GIVEN** a TCP client has completed Routing Activation
- **AND** the client has received a seed for SecurityAccess level `1`
- **WHEN** the client sends a DoIP diagnostic message with UDS payload beginning with `0x27, 0x02` and the correct key
- **THEN** the payload SHALL be dispatched to the SecurityAccess service
- **AND** the client SHALL receive a DoIP diagnostic response containing the UDS `0x67` key response
- **AND** SecurityAccess level `1` SHALL become unlocked

#### Scenario: DoIP 层不实现 SecurityAccess 业务
- **GIVEN** task-018 is implemented
- **WHEN** the DoIP diagnostic message handler is inspected
- **THEN** it SHALL continue forwarding UDS payloads to the dispatcher
- **AND** it SHALL NOT compute seeds or keys directly
- **AND** it SHALL NOT mutate SecurityAccess runtime state directly

### Requirement: Scope Boundaries

The implementation SHALL remain limited to built-in SecurityAccess MVP behavior and protected DID/Routine access checks.

#### Scenario: 不加载 DLL
- **GIVEN** task-018 is implemented
- **WHEN** SecurityAccess algorithm resolution is inspected
- **THEN** it SHALL NOT load DLL files
- **AND** it SHALL NOT use reflection or native loading to invoke external security algorithms

#### Scenario: 不实现 OEM 真实算法
- **GIVEN** task-018 is implemented
- **WHEN** supported SecurityAccess algorithms are inspected
- **THEN** they SHALL be limited to documented built-in example algorithms
- **AND** they SHALL NOT include OEM-specific reverse-engineered or proprietary algorithms

#### Scenario: 不实现 `0x84`
- **GIVEN** task-018 is implemented
- **WHEN** the UDS service registry is inspected
- **THEN** service `0x84` SecuredDataTransmission SHALL NOT be implemented by this change
- **AND** unsupported `0x84` requests SHALL continue to use existing unsupported service behavior unless another validated change defines them

#### Scenario: 不扩大到其他诊断流程
- **GIVEN** task-018 is implemented
- **WHEN** changed files are inspected
- **THEN** the change SHALL NOT implement Flash flows
- **AND** it SHALL NOT add ODX/PDX import
- **AND** it SHALL NOT add PCAP or TLS features
- **AND** it SHALL NOT perform unrelated refactoring

