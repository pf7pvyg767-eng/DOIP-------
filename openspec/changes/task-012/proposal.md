# Proposal: 最小 ECU 状态和 `0x10`/`0x3E`

**Change ID:** `task-012`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

`task-011` 已建立 UDS request/response、NRC 和 dispatcher 基础，但系统仍缺少可被 UDS 业务服务共享的最小 ECU runtime state。后续 DID、DTC、Routine、SecurityAccess 和刷写任务需要知道当前诊断会话；当前阶段只需要先支持 DiagnosticSessionControl (`0x10`) 与 TesterPresent (`0x3E`) 的最小可用行为。

本 change 需要在不引入完整 ECU 状态机、不实现安全访问、不实现 ResponsePending 的前提下，定义最小 ECU runtime state，注册 `0x10` 与 `0x3E` 服务，让会话切换可通过 UDS 正响应和运行时事件被验证，并在 `0x10` 正响应中返回基础 P2/P2* 参数。

## Proposed Solution

- 在 Core 或 UDS 可复用边界定义最小 ECU runtime state，至少包含 logical address、当前 session、security locked 状态摘要和最近 TesterPresent 时间。
- 定义诊断会话枚举或等价模型，覆盖默认会话、扩展会话和编程会话。
- 实现 `0x10` DiagnosticSessionControl 服务，支持子功能 `0x01`、`0x03`、`0x02`，分别切换默认、扩展、编程会话。
- `0x10` 正响应 SHALL 使用 `0x50 subFunction P2 P2*` 形式返回，P2/P2* 使用明确的基础参数值，并由测试固定。
- 对未知或不支持的 `0x10` 子功能返回明确 NRC，优先复用现有 NRC 模型。
- 实现 `0x3E` TesterPresent 服务，支持 `3E 00` 返回正响应 `7E 00`，并更新最近 TesterPresent 时间。
- 会话状态变化 SHALL 写入现有 runtime event 管道，事件中可看到旧 session、新 session、logical address 或连接摘要。
- 增加 UDS service 单元测试与 DoIP Routing Activation 后的集成测试，验证 `10 01`、`10 03`、`10 02`、`3E 00` 和会话事件。

## Scope

### In Scope

- 定义最小 ECU runtime state。
- 定义或复用 session state，覆盖默认、扩展、编程会话。
- 实现 `0x10` DiagnosticSessionControl 的最小规格要求。
- 实现 `0x3E` TesterPresent 的最小规格要求。
- 会话状态变化写入现有结构化事件管道。
- `0x10` 正响应返回基础 P2/P2* 参数。
- 为 `10 01`、`10 03`、`10 02`、`3E 00` 和会话事件增加测试。

### Out of Scope

- 不实现 TesterPresent 超时回退。
- 不实现 SecurityAccess。
- 不实现 `0x78 ResponsePending`。
- 不实现 P2/P2* 定时器、超时调度或异步 pending 策略。
- 不实现 DID、DTC、Routine、刷写或其他 UDS 业务服务。
- 不实现完整 ECU 状态机、复杂会话权限矩阵或跨 ECU 多实例状态管理。
- 不新增 Web UI 页面、诊断服务编辑器或手动异常注入能力。

## Open Questions

- 原始 task 未指定 P2/P2* 的具体数值。实现应选择明确、稳定、可测试的基础参数，并在代码或测试中固定；不得扩展为真实超时调度。
- 原始 task 未指定 `0x10` 未知子功能的 NRC。实现应沿用现有 `NegativeResponse` 模型，选择符合 UDS 语义且项目已有或可最小新增的 NRC，并用测试固定。
- 原始 task 未指定 ECU runtime state 的生命周期。实现应采用当前 Host/UDS dispatcher 可注入的最小共享运行时状态，不引入持久化或配置迁移。
- 原始 task 未指定会话事件字段命名。实现应复用现有 runtime event 结构，至少包含旧 session、新 session 和连接或 logical address 摘要。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Yes | 新增最小 ECU runtime state 与 session 状态模型。 |
| Protocols.Uds | Yes | 新增 `0x10` 和 `0x3E` 服务并注册到 dispatcher。 |
| Protocols.Doip | Maybe | 复用 task-011 diagnostic forwarding；仅为集成测试覆盖正响应路径。 |
| Host | Yes | 注册最小 ECU runtime state 与 UDS 服务。 |
| Runtime Events | Yes | 发布会话变化事件，复用现有事件管道。 |
| WebApi | No | 不新增 API。 |
| WebConsole | No | 不新增 UI；现有日志视图可查看事件。 |
| Tests | Yes | 增加 UDS service、runtime state、DoIP 集成与 scope check 测试。 |

## Architecture Considerations

- ECU runtime state 应保持最小、内存态、可测试，不应承担后续 SecurityAccess、DID/DTC/Routine 或刷写状态职责。
- `0x10` 和 `0x3E` 应作为 `IUdsService` 注册到现有 UDS dispatcher，避免在 DoIP 层实现 UDS 业务逻辑。
- DoIP 层继续只负责 diagnostic message payload 转发和响应封装，不应了解具体 session 子功能。
- P2/P2* 仅作为 `0x10` 正响应参数返回，不应启动计时器或 ResponsePending 调度。
- TesterPresent 仅处理 `3E 00` 正响应和最近请求时间记录，不应实现超时回退。
- 会话事件应复用 `RuntimeEvent` 管道，并避免记录完整敏感 payload。

## Acceptance Criteria

- [ ] `10 01` 可以切换到默认会话并返回正响应。
- [ ] `10 03` 可以切换到扩展会话并返回正响应。
- [ ] `10 02` 可以切换到编程会话并返回正响应。
- [ ] `0x10` 正响应包含基础 P2/P2* 参数。
- [ ] `3E 00` 返回正响应。
- [ ] `3E 00` 更新最近 TesterPresent 时间或等价最小状态。
- [ ] 会话变化可以在运行时事件中看到。
- [ ] Scope check 确认未实现 TesterPresent 超时回退、SecurityAccess、ResponsePending、DID/DTC/Routine 或刷写服务。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 最小 ECU state 被扩展成完整状态机 | Medium | High | 在 spec 和 tasks 中限定字段和行为，只允许 session、security locked 摘要和 TesterPresent 时间。 |
| `0x10` 实现顺手加入后续服务权限逻辑 | Medium | High | 明确本 change 不实现 DID/DTC/Routine/SecurityAccess 权限矩阵，scope check 覆盖。 |
| P2/P2* 被误实现为真实定时器 | Medium | Medium | 规格限定为正响应基础参数，不实现超时、ResponsePending 或调度。 |
| TesterPresent 被误实现为超时回退 | Medium | Medium | 规格只要求 `3E 00` 正响应和时间记录，超时回退列入 out of scope。 |
| 会话事件字段与现有日志结构不一致 | Low | Medium | 复用现有 runtime event 结构，只要求稳定摘要字段和测试可见性。 |
