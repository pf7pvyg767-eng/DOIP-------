# Proposal: SecurityAccess 内置算法和安全状态

**Change ID:** `task-018`
**Created:** 2026-05-17
**Completed:** 2026-05-17
**Status:** Implementation Complete

---

## Problem Statement

当前模拟器已经具备基础 ECU 会话状态、DID 读取、RoutineControl 固定响应、通信控制和 DTC 设置等能力，但安全访问仍停留在预留配置或未实现状态。诊断调试人员需要一个可测试、可配置、可重复的 `0x27` SecurityAccess MVP，用来在不加载 DLL、不实现 OEM 真实算法的前提下验证 seed/key 主路径、失败计数、锁定状态，以及受保护 DID/Routine 的访问门控。

本 change 仅补齐内置示例算法和内存安全状态，作为后续插件算法、真实 OEM 算法或更完整安全策略之前的最小闭环。

## Proposed Solution

- 扩展 SecurityAccess 配置模型，支持安全等级、seed request sub-function、key send sub-function、内置算法类型/参数、失败次数上限和锁定时间。
- 实现 UDS `0x27` SecurityAccess 主路径：请求 seed 返回非空 seed，发送正确 key 后解锁对应安全等级。
- 提供至少一种内置示例算法，例如 `builtin-xor` 或 `builtin-add`，算法结果必须可预测、可测试、只依赖 seed 和配置参数。
- 在 ECU 运行时维护每个安全等级的解锁状态、最近 seed、失败计数和锁定截止时间。
- 对错误 key 返回明确 NRC，并累计失败次数；达到配置上限后进入锁定状态，在锁定期间拒绝相关 seed/key 请求。
- 将 SecurityAccess 状态接入已存在的 DID/Routine 安全要求，使受保护 DID/Routine 在未解锁时失败，在解锁对应等级后成功。

## Scope

### In Scope

- SecurityAccess 配置模型和基础验证。
- `0x27` 请求 seed / 发送 key 主路径。
- 内置示例算法，例如 XOR 或加法算法。
- 每个安全等级的内存解锁状态、失败次数和锁定时间。
- 错误 key NRC、失败次数累计、锁定 NRC。
- DID/Routine 访问控制对安全等级的检查。
- 针对 seed/key 正常流、错误 key、锁定、受保护 DID/Routine 的单元/集成测试。

### Out of Scope

- 不加载 DLL。
- 不实现 OEM 真实算法。
- 不实现 `0x84` SecuredDataTransmission。
- 不实现安全状态持久化、跨进程恢复或多客户端隔离策略。
- 不实现复杂随机数/密码学强度保证、HSM、证书或 TLS 绑定。
- 不新增 Web 安全解锁控制台或算法编辑 UI，除非实现受影响状态只读展示所必需。
- 不扩大到 Flash、ODX/PDX、PCAP/TLS 或其他诊断流程。

## Open Questions

- task 未指定锁定期间的精确 NRC。实现应采用项目现有 NRC 枚举中最贴近 ISO 语义的 `requiredTimeDelayNotExpired` 或等价清晰错误，并在测试中固定该映射。
- task 未指定错误 key 的精确 NRC。实现应采用项目现有负响应模型中最贴近 `invalidKey` 的错误；如当前枚举缺失，应以最小范围补齐或使用等价明确 NRC。
- task 未指定 seed 是否必须随机。为保证测试可重复，MVP 可使用可预测但非空的 seed 生成方式；不得声称具备真实安全强度。
- task 未指定 DID/Routine 配置中安全要求字段的最终名称。实现应复用现有 DID/Routine 配置中已有安全等级字段；如字段缺失，只做最小扩展以表达所需安全等级。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 扩展 SecurityAccess 配置，必要时补充 DID/Routine 的安全等级要求字段。 |
| Core Runtime State | Yes | 维护安全等级解锁状态、seed、失败计数和锁定截止时间。 |
| Security Algorithm | Yes | 增加内置示例算法，不加载 DLL，不接入 OEM 算法。 |
| Protocols.Uds | Yes | 注册并实现 `0x27` SecurityAccess 主路径及相关 NRC。 |
| DID Service | Yes | 在读取受保护 DID 前检查所需安全等级。 |
| Routine Service | Yes | 在调用受保护 Routine 前检查所需安全等级。 |
| Web/API | No | 本 change 不要求新增管理 UI；如已有状态 API 需要只读暴露，应保持最小化。 |
| Tests | Yes | 覆盖 seed/key、错误 key、锁定、DID/Routine 门控和 scope check。 |

## Architecture Considerations

- SecurityAccess 服务应继续通过现有 UDS dispatcher 注册，DoIP 层只转发诊断 payload，不解析 seed/key 或安全配置。
- 安全状态应是运行时内存状态，不写回配置文件，不跨 Host 重启持久化。
- 内置算法应以接口或清晰服务封装，便于后续 task 接入 DLL 或 OEM 算法，但本 change 不实现任何外部加载。
- DID/Routine 的安全检查应复用共享 ECU runtime security state，避免 DID/Routine 各自维护独立解锁标志。
- 锁定计时应可测试，优先通过可注入时钟或等价方式验证锁定时间，不引入后台调度器。

## Acceptance Criteria

- [x] 请求 seed 返回非空 seed。
- [x] 正确 key 解锁指定安全等级。
- [x] 错误 key 返回 NRC，并累计失败次数。
- [x] 达到失败次数后进入锁定状态。
- [x] 解锁状态影响受保护 DID/Routine：未解锁失败，解锁后成功。
- [x] Scope check 确认未加载 DLL、未实现 OEM 真实算法、未实现 `0x84`，且未扩大到 Flash、ODX/PDX、PCAP/TLS 或其他诊断流程。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| MVP 被误解为真实安全算法 | Medium | High | 文档和 spec 明确内置算法仅为示例、可测试，不提供 OEM 真实安全能力。 |
| 失败计数和锁定状态难以稳定测试 | Medium | Medium | 使用可控时钟或等价注入点验证锁定截止时间，避免依赖真实等待。 |
| DID/Routine 安全字段与现有配置不一致 | Medium | Medium | 优先复用现有字段；若需新增字段，只扩展最小数据契约并补充验证。 |
| NRC 映射不统一 | Medium | Medium | 复用现有负响应模型，并在 open questions 和测试中固定项目内映射。 |
| 误扩展到 DLL/OEM/0x84 | Low | High | 在 scope、spec 和测试计划中加入明确排除项和 scope check。 |

