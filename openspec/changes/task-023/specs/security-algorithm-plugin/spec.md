# Spec: DLL 安全算法插件 MVP

**Change ID:** `task-023`
**Status:** Draft

---

## ADDED Requirements

### Requirement: Security Plugin ABI Documentation

The system SHALL document a stable MVP C ABI for external SecurityAccess DLL plugins.

#### Scenario: ABI 文档定义导出函数
- **GIVEN** task-023 is implemented
- **WHEN** `docs/SecurityPlugin-ABI.md` is inspected
- **THEN** the document SHALL define `DoipSec_GetAbiVersion`
- **AND** it SHALL define `DoipSec_GenerateSeed`
- **AND** it SHALL define `DoipSec_VerifyKey`
- **AND** it SHALL describe parameter meanings, return values, buffer length handling, and ABI version compatibility.

#### Scenario: ABI 文档定义版本策略
- **GIVEN** a SecurityAccess plugin DLL exports `DoipSec_GetAbiVersion`
- **WHEN** the loader calls the function
- **THEN** the loader SHALL compare the returned value with the supported MVP ABI version
- **AND** it SHALL reject unsupported versions with a clear error
- **AND** the ABI document SHALL state the supported version.

#### Scenario: ABI 文档限定示例算法性质
- **GIVEN** the ABI documentation describes the sample plugin
- **WHEN** a reader inspects the document
- **THEN** the document SHALL state that the sample algorithm is deterministic test code
- **AND** it SHALL NOT claim to implement a real OEM or cryptographically strong algorithm.

### Requirement: Security Plugin Configuration

The system SHALL provide configuration for enabling and locating a SecurityAccess DLL plugin.

#### Scenario: 加载插件配置字段
- **GIVEN** a JSON simulator configuration contains a `securityPlugin` object
- **WHEN** the configuration is loaded
- **THEN** the system SHALL load `enabled`
- **AND** it SHALL load `dllPath`
- **AND** it SHALL load `timeoutMs`.

#### Scenario: 插件禁用时保持内置算法
- **GIVEN** `securityPlugin.enabled` is `false`
- **WHEN** SecurityAccess algorithms are resolved
- **THEN** the system SHALL use the existing built-in `0x27` algorithm behavior
- **AND** it SHALL NOT load a DLL
- **AND** existing SecurityAccess tests SHALL remain compatible.

#### Scenario: 插件启用时校验路径
- **GIVEN** `securityPlugin.enabled` is `true`
- **WHEN** simulator configuration is validated or plugin loading starts
- **THEN** the system SHALL require `securityPlugin.dllPath`
- **AND** missing or empty `dllPath` SHALL produce a clear field-specific error
- **AND** the service SHALL NOT crash.

#### Scenario: 插件 timeout 配置合法性
- **GIVEN** `securityPlugin.timeoutMs` is configured
- **WHEN** simulator configuration is validated
- **THEN** the system SHALL reject non-positive timeout values
- **AND** the validation error SHALL identify `securityPlugin.timeoutMs`.

### Requirement: Security Plugin Loading And ABI Check

The system SHALL load the configured DLL plugin and verify ABI compatibility before using it for SecurityAccess.

#### Scenario: 示例 DLL 可加载
- **GIVEN** `securityPlugin.enabled` is `true`
- **AND** `securityPlugin.dllPath` points to the built sample SecurityAccess plugin DLL
- **WHEN** the plugin loader starts
- **THEN** the loader SHALL load the DLL
- **AND** it SHALL resolve all required ABI functions
- **AND** it SHALL report the plugin as available for SecurityAccess.

#### Scenario: DLL 文件缺失
- **GIVEN** `securityPlugin.enabled` is `true`
- **AND** `securityPlugin.dllPath` points to a missing file
- **WHEN** the plugin loader starts
- **THEN** loading SHALL fail with a clear error identifying the missing DLL path
- **AND** the Host service SHALL NOT crash
- **AND** a diagnostic event or log entry SHALL describe the failure.

#### Scenario: ABI 版本不匹配
- **GIVEN** a plugin DLL can be loaded
- **AND** `DoipSec_GetAbiVersion` returns an unsupported version
- **WHEN** the plugin loader checks compatibility
- **THEN** the plugin SHALL be rejected
- **AND** the error SHALL include the expected and actual ABI version
- **AND** the Host service SHALL NOT crash.

#### Scenario: 必需入口函数缺失
- **GIVEN** a plugin DLL can be loaded
- **AND** one or more required ABI functions are missing
- **WHEN** the plugin loader resolves exports
- **THEN** the plugin SHALL be rejected
- **AND** the error SHALL identify the missing function
- **AND** the Host service SHALL NOT crash.

### Requirement: Plugin Based Seed Generation

The SecurityAccess service SHALL use the plugin to generate `0x27` seeds when the plugin is enabled and loaded.

#### Scenario: `0x27` seed 来自 DLL
- **GIVEN** SecurityAccess level `1` is configured
- **AND** the SecurityAccess plugin is enabled and loaded successfully
- **WHEN** the tester sends a seed request for the configured seed sub-function
- **THEN** the SecurityAccess service SHALL call `DoipSec_GenerateSeed`
- **AND** the positive `0x67` response SHALL contain the seed bytes returned by the DLL
- **AND** the generated seed SHALL be stored in the existing SecurityAccess runtime state for the level.

#### Scenario: seed 生成失败
- **GIVEN** the SecurityAccess plugin is enabled and loaded successfully
- **AND** `DoipSec_GenerateSeed` returns a failure code
- **WHEN** the tester requests a seed
- **THEN** the SecurityAccess service SHALL return a negative response for service `0x27` or a clear runtime error according to existing project conventions
- **AND** it SHALL publish or log the plugin failure reason
- **AND** the Host service SHALL NOT crash.

#### Scenario: seed 输出长度非法
- **GIVEN** `DoipSec_GenerateSeed` returns success
- **AND** the reported seed length is zero or exceeds the configured output buffer
- **WHEN** the SecurityAccess service validates the plugin output
- **THEN** the seed request SHALL fail with a clear plugin output error
- **AND** no invalid seed SHALL be stored as active state.

### Requirement: Plugin Based Key Verification

The SecurityAccess service SHALL use the plugin to verify `0x27` keys when the plugin is enabled and loaded.

#### Scenario: 正确 key 解锁成功
- **GIVEN** a SecurityAccess level has an active seed generated by the plugin
- **AND** the tester sends a key accepted by `DoipSec_VerifyKey`
- **WHEN** the SecurityAccess service processes the key sub-function
- **THEN** it SHALL return a positive `0x67` response
- **AND** it SHALL mark that SecurityAccess level as unlocked
- **AND** it SHALL reset failed attempt state according to existing SecurityAccess behavior.

#### Scenario: 错误 key 返回 NRC
- **GIVEN** a SecurityAccess level has an active seed generated by the plugin
- **AND** the tester sends a key rejected by `DoipSec_VerifyKey`
- **WHEN** the SecurityAccess service processes the key sub-function
- **THEN** it SHALL return a negative response for service `0x27`
- **AND** the NRC SHALL follow the existing invalid-key project convention
- **AND** the security level SHALL remain locked
- **AND** failed attempt tracking SHALL follow the existing SecurityAccess behavior.

#### Scenario: key 校验调用失败
- **GIVEN** a SecurityAccess level has an active seed generated by the plugin
- **AND** `DoipSec_VerifyKey` returns a plugin failure code that is distinct from invalid key
- **WHEN** the SecurityAccess service processes the key sub-function
- **THEN** it SHALL return a negative response or clear runtime error according to existing project conventions
- **AND** it SHALL publish or log the plugin failure reason
- **AND** the Host service SHALL NOT crash.

### Requirement: Sample Security Plugin Project

The repository SHALL provide a sample DLL project implementing the documented SecurityAccess plugin ABI.

#### Scenario: 示例 DLL 工程存在
- **GIVEN** task-023 is implemented
- **WHEN** `samples/SecurityPluginExample/` is inspected
- **THEN** the sample project SHALL build a DLL
- **AND** it SHALL export the documented ABI functions
- **AND** it SHALL use a deterministic sample seed/key algorithm.

#### Scenario: 示例 DLL 支持自动化测试
- **GIVEN** the sample DLL project has been built
- **WHEN** automated integration tests configure `securityPlugin.dllPath` to that DLL
- **THEN** the simulator SHALL load the sample DLL
- **AND** tests SHALL be able to compute a correct key for the returned seed
- **AND** tests SHALL be able to produce an incorrect key that returns a NRC.

### Requirement: Security Plugin Events And Logs

The system SHALL emit diagnostic logs or runtime events for plugin lifecycle and failure outcomes.

#### Scenario: 插件加载成功事件
- **GIVEN** the SecurityAccess plugin loads successfully
- **WHEN** the loader completes ABI validation
- **THEN** the system SHALL publish or log a plugin loaded event
- **AND** the event SHALL include the DLL path or sanitized path summary
- **AND** it SHALL include the ABI version.

#### Scenario: 插件错误事件包含原因
- **GIVEN** plugin loading, ABI validation, seed generation, or key verification fails
- **WHEN** the failure is handled
- **THEN** the system SHALL publish or log a diagnostic failure event
- **AND** the event SHALL include a clear reason
- **AND** it SHALL NOT include key material or other secret data.

### Requirement: Security Plugin Scope Boundaries

The task-023 implementation SHALL remain limited to the SecurityAccess DLL plugin MVP.

#### Scenario: 不实现进程隔离
- **GIVEN** task-023 is implemented
- **WHEN** plugin execution architecture is inspected
- **THEN** the change SHALL NOT add a separate plugin host process
- **AND** it SHALL NOT claim process isolation or sandbox security.

#### Scenario: 不支持脚本插件
- **GIVEN** task-023 is implemented
- **WHEN** supported plugin types are inspected
- **THEN** the change SHALL support the documented DLL ABI only
- **AND** it SHALL NOT add JavaScript, Python, Lua, C# script, or other script plugin execution.

#### Scenario: 不实现企业真实 OEM 算法
- **GIVEN** task-023 is implemented
- **WHEN** sample and production plugin code is inspected
- **THEN** the change SHALL NOT include a proprietary OEM seed/key algorithm
- **AND** it SHALL NOT claim compatibility with a real vehicle OEM security algorithm.

#### Scenario: 不完整实现 `0x84`
- **GIVEN** task-023 is implemented
- **WHEN** UDS services are inspected
- **THEN** the change SHALL NOT implement full `0x84` SecuredDataTransmission encryption or decryption
- **AND** unsupported `0x84` behavior SHALL remain governed by existing project behavior unless another validated change defines it.

### Requirement: Security Plugin Verification

The task-023 implementation SHALL include focused verification for the DLL plugin MVP.

#### Scenario: 插件缺失测试
- **GIVEN** automated tests configure `securityPlugin.enabled` as `true`
- **AND** `securityPlugin.dllPath` points to a missing file
- **WHEN** plugin loading is exercised
- **THEN** the test SHALL verify a clear missing DLL error
- **AND** it SHALL verify the service does not crash.

#### Scenario: ABI 不匹配测试
- **GIVEN** automated tests provide a DLL with unsupported ABI version or a loader test double
- **WHEN** plugin loading is exercised
- **THEN** the test SHALL verify a clear ABI mismatch error
- **AND** it SHALL verify the service does not crash.

#### Scenario: 示例插件 seed/key 集成测试
- **GIVEN** automated tests configure the built sample SecurityAccess plugin
- **WHEN** the tester performs `0x27` seed request and sends the correct key
- **THEN** the test SHALL verify the seed came from the DLL
- **AND** it SHALL verify unlock succeeds.

#### Scenario: 错误 key NRC 测试
- **GIVEN** automated tests configure the built sample SecurityAccess plugin
- **AND** a seed has been generated by the DLL
- **WHEN** the tester sends an incorrect key
- **THEN** the test SHALL verify a negative `0x27` response
- **AND** it SHALL verify the level remains locked.

## MODIFIED Requirements

### Requirement: SecurityAccess Service `0x27`

The UDS protocol layer SHALL continue to register service `0x27` SecurityAccess and SHALL use the configured SecurityAccess algorithm provider, including the DLL plugin provider when enabled and loaded.

#### Scenario: 插件启用时 `0x27` 使用 DLL 算法
- **GIVEN** SecurityAccess service `0x27` is registered
- **AND** the DLL plugin provider is enabled and loaded
- **WHEN** the service processes seed and key sub-functions
- **THEN** it SHALL use the plugin provider for seed generation and key verification
- **AND** DoIP diagnostic forwarding SHALL remain unchanged.

#### Scenario: 插件禁用时 `0x27` 保持内置算法
- **GIVEN** SecurityAccess service `0x27` is registered
- **AND** the DLL plugin provider is disabled
- **WHEN** the service processes seed and key sub-functions
- **THEN** it SHALL use the existing built-in algorithm behavior
- **AND** existing SecurityAccess state, failed attempt, lockout, DID protection, and Routine protection behavior SHALL remain unchanged.

## REMOVED Requirements

None.
