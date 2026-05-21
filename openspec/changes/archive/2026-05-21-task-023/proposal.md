# Proposal: DLL 安全算法插件 MVP

**Change ID:** `task-023`
**Created:** 2026-05-19
**Status:** Implementation Complete
**Completed:** 2026-05-19

---

## Problem Statement

当前 `task-018` 已提供 SecurityAccess `0x27` 内置 seed/key MVP 能力，但无法通过外部 DLL 替换算法边界。后续集成测试和用户扩展需要一个最小、可文档化、可验证的 DLL 插件机制，使模拟器可以从配置加载安全算法 DLL，由 DLL 生成 seed 并校验 key，同时在 DLL 缺失、ABI 不匹配或调用失败时返回清晰错误且不导致服务崩溃。

本 change 只覆盖 `task-023` 的 DLL 安全算法插件 MVP proposal：定义 DLL ABI 文档，设计插件加载和版本检查，用插件替代内置 `0x27` 算法，提供示例 DLL 工程，并覆盖插件缺失、版本不匹配和调用失败的错误处理。

## Proposed Solution

- 新增 `docs/SecurityPlugin-ABI.md`，定义 C ABI、函数签名、ABI 版本、参数含义、返回码、缓冲区约定、线程安全约定和错误处理边界。
- 在 Security 插件边界设计 `SecurityPluginConfig` 与 `SecurityPluginLoader`，从配置读取 `enabled`、`dllPath`、`timeoutMs` 等字段，加载 DLL 并检查 `DoipSec_GetAbiVersion`。
- 在 SecurityAccess 算法解析路径中，当插件启用且加载成功时，使用 DLL 的 `DoipSec_GenerateSeed` 和 `DoipSec_VerifyKey` 替代内置 `0x27` 算法；插件未启用时保持现有内置算法行为。
- 新增 `samples/SecurityPluginExample/` 示例 DLL 工程，提供确定性的非 OEM 示例算法，便于自动化测试验证 seed/key 主路径。
- 将 DLL 缺失、ABI 版本不匹配、入口函数缺失、函数返回失败、seed/key 长度非法和调用超时设计为可诊断错误；运行时服务不得因插件错误崩溃。

## Scope

### In Scope

- 定义 DLL ABI 文档，覆盖 `DoipSec_GetAbiVersion`、`DoipSec_GenerateSeed`、`DoipSec_VerifyKey`。
- 设计插件加载、入口函数解析和 ABI 版本检查。
- 设计 `SecurityPluginConfig` 配置契约，包括启用开关、DLL 路径和调用超时。
- 设计 `0x27` SecurityAccess seed/key 主路径使用插件算法替代内置算法。
- 提供示例 DLL 工程和确定性示例算法，支持自动化测试。
- 处理 DLL 缺失、ABI 不匹配、入口函数缺失、调用失败、输出长度非法和调用超时。
- 为插件加载、版本检查、示例 DLL、正确 key 解锁、错误 key 返回 NRC、故障不崩溃提供测试设计。

### Out of Scope

- 不实现进程隔离或沙箱化插件执行。
- 不支持脚本插件。
- 不实现企业真实 OEM 算法、专有算法或逆向算法。
- 不完整实现 UDS `0x84` SecuredDataTransmission 加解密。
- 不新增 SecurityAccess 以外的诊断服务能力。
- 不改变未启用插件时的内置 `0x27` 行为。
- 不引入证书/TLS、ODX/PDX、异常注入、PCAP 高级能力或报文搜索。

## Open Questions

- ABI 版本常量的具体数值未在任务中指定；本 proposal 建议 MVP 使用 `1` 作为唯一受支持 ABI 版本。
- `timeoutMs` 的强制实现方式需在 Apply 阶段结合当前进程内调用模型确定；由于本 task 明确排除进程隔离，超时只能作为可观测和防护边界设计，不能保证强制终止恶意 native 代码。
- 插件调用失败时 `0x27` 的具体 NRC 应复用项目现有 SecurityAccess 错误约定；若现有约定没有插件错误映射，Apply 阶段需选择最接近的通用 NRC 并记录。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Documentation | Yes | 新增 DLL ABI 文档，描述 C ABI、版本、返回码和示例。 |
| Configuration | Yes | 新增或补齐 Security plugin 配置契约和校验。 |
| Security | Yes | 新增插件 loader、ABI 检查、函数绑定和插件算法适配器。 |
| UDS `0x27` | Yes | 插件启用时 seed/key 由 DLL 生成和校验；未启用时保持内置算法。 |
| Samples | Yes | 新增示例 DLL 工程，用确定性算法支持测试。 |
| Runtime Events/Logs | Yes | 插件加载、版本不匹配、调用失败和 fallback/拒绝路径需要清晰日志或事件。 |
| Web UI | No | 本 task 不要求新增 UI；可通过既有日志观察插件错误。 |
| Tests | Yes | 覆盖插件加载、ABI 不匹配、seed/key 成功、错误 key NRC 和故障不崩溃。 |

## Architecture Considerations

- 插件能力应限制在 SecurityAccess 算法边界，DoIP 层继续只转发 UDS payload，不直接加载 DLL 或计算 seed/key。
- ABI 文档应使用稳定 C ABI，避免要求插件引用模拟器内部 .NET 类型。
- Loader 应把 native 入口函数解析、ABI 校验和错误归一化封装在 Security 插件边界内，SecurityAccess 服务只消费统一算法接口。
- 示例 DLL 必须是测试用确定性算法，不声明真实安全强度或 OEM 兼容性。
- 插件错误必须可诊断且不崩溃 Host；但由于排除进程隔离，本 MVP 不承诺防护恶意或阻塞 native 插件。

## Acceptance Criteria

- [x] 示例 DLL 可被配置路径加载。
- [x] `0x27` seed 来源于 DLL 的 `DoipSec_GenerateSeed`。
- [x] 使用 DLL 约定计算出的正确 key 可解锁对应 SecurityAccess level。
- [x] 错误 key 返回 `0x27` 负响应 NRC，且对应安全级别保持锁定。
- [x] DLL 缺失时错误清晰，服务不崩溃。
- [x] ABI 版本不匹配时错误清晰，服务不崩溃。
- [x] 插件调用失败时返回清晰错误或 NRC，服务不崩溃。
- [x] Scope check 确认未实现进程隔离、脚本插件、企业真实 OEM 算法或完整 `0x84` 加解密。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Native DLL 崩溃或阻塞影响 Host | Medium | High | 明确 MVP 不做进程隔离；记录风险，加载和普通失败路径必须可诊断，超时只作为进程内防护设计。 |
| ABI 约定不清导致示例和 loader 不兼容 | Medium | High | 先编写 ABI 文档和自动化测试，固定 ABI 版本、返回码和长度约定。 |
| 插件算法绕过现有 SecurityAccess 状态 | Medium | High | 插件只负责 seed/key 算法，锁定、失败次数、NRC 和状态仍由现有 SecurityAccess 服务管理。 |
| 错误 key 或插件失败返回不符合 UDS 语义 | Medium | Medium | 复用现有 `0x27` 负响应和 failed attempt 约定，并增加错误 key / 调用失败测试。 |
| 示例 DLL 被误解为 OEM 算法 | Low | Medium | 文档和命名明确为 deterministic sample algorithm，不提供真实 OEM 算法。 |
