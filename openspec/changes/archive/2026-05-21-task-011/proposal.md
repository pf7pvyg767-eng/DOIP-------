# Proposal: UDS 分发框架和 NRC 响应模型

**Change ID:** `task-011`
**Created:** 2026-05-16
**Status:** Implementation Complete
**Completed:** 2026-05-16

---

## Problem Statement

项目已经具备 DoIP frame codec、UDP 车辆发现、TCP 连接管理和 Routing Activation 能力，但 Routing Activation 之后的 DoIP diagnostic message 还没有进入统一 UDS 分发入口。后续 `0x10`、`0x3E`、DID、DTC、Routine、SecurityAccess 和刷写相关任务都需要稳定的 UDS 请求/响应模型、服务注册分发机制、负响应 NRC 编码，以及最小的诊断消息转发链路。

本 change 只建立 UDS 分发基础和 NRC 响应模型：DoIP diagnostic message payload 可以被转换为 `UdsRequest` 并交给 dispatcher；未注册或未支持的 SID 必须返回 `0x7F SID 0x11`；基础请求长度错误可返回 `0x13`；UDS 请求/响应摘要进入现有事件日志管道。范围不得扩展到具体 UDS 正响应服务、ECU 状态机细节或 SecurityAccess。

## Proposed Solution

- 在 `DoipSimulator.Protocols.Uds` 中定义 `UdsRequest`、`UdsResponse`、`NegativeResponse` 和基础 NRC 常量或枚举。
- 定义 `IUdsService` 和 `UdsDispatcher`，支持按 SID 注册服务处理器并分发请求。
- 建立基础 `UdsContext` 或等价上下文对象，仅承载本 change 分发所需的连接/地址/配置引用，不实现 ECU 状态机。
- 将 UDS 字节响应模型标准化：正响应由后续服务实现提供；负响应编码为 `0x7F, requestSid, nrc`。
- 在 DoIP diagnostic message handler 或等价 TCP DoIP 协议处理链路中，把 Routing Activation 后收到的 diagnostic message payload 转发给 UDS dispatcher，并把 UDS 响应包装回 DoIP diagnostic message response。
- 为未注册 SID 返回 `NegativeResponse`，NRC 为 `0x11` ServiceNotSupported。
- 为基础格式错误或长度不足请求返回 `NegativeResponse`，NRC 为 `0x13` IncorrectMessageLengthOrInvalidFormat。
- 通过现有 `RuntimeEvent` 管道发布 UDS 请求、响应和错误摘要事件，并复用现有结构化日志和 Web 日志 UI。
- 增加 UDS dispatcher 单元测试、负响应编码测试、服务注册分发测试，以及 TCP Routing Activation 后发送未知 UDS SID 得到 NRC 的集成测试。

## Scope

### In Scope

- 定义 `UdsRequest` 数据契约。
- 定义 `UdsResponse` 响应抽象或等价契约。
- 定义 `NegativeResponse` 和基础 NRC 常量，包括 `0x11` 与 `0x13`。
- 实现 `IUdsService` 服务处理器接口。
- 实现 UDS dispatcher，支持服务注册和按 SID 分发。
- DoIP diagnostic message payload 转发到 UDS dispatcher。
- 未支持或未注册 SID 返回 `0x7F SID 0x11`。
- 请求长度错误或无法形成有效 UDS 请求时返回 `0x7F SID 0x13`。
- UDS 请求/响应/错误事件写入现有结构化日志。
- UDS 请求/响应/错误事件可通过现有 Web 日志 UI 看到。
- 增加单元测试、集成测试和 scope check。

### Out of Scope

- 不实现具体 UDS 服务正响应。
- 不实现 `0x10`、`0x3E`、DID、DTC、Routine、刷写或其他业务服务。
- 不实现 ECU 状态机细节。
- 不实现 Session 状态迁移、P2/P2* 计时或 TesterPresent 超时。
- 不实现 SecurityAccess。
- 不实现 `0x78 ResponsePending` 策略。
- 不实现手动 NRC、异常注入、自定义 UDS 响应或 Web 编辑能力。
- 不新增 Web UI 页面、复杂图表或 UDS 管理界面。
- 不改变 TCP Routing Activation、UDP discovery、TLS、PCAP 或外部 observability 范围。

## Open Questions

- 原始 task 未明确 DoIP diagnostic message response 中 tester/ECU logical address 的方向映射细节。实现应沿用 task-010 已建立的连接 routing activation 状态和现有 DoIP payload 契约，并通过集成测试固定响应地址方向。
- 原始 task 未明确 dispatcher 对多个响应的发送顺序和同步/异步策略。实现应采用 `IUdsService.HandleAsync` 返回顺序作为响应顺序；本 change 只需支持返回一个或多个 UDS 响应，不引入 ResponsePending 调度。
- 原始 task 未明确空 payload 与仅 SID payload 的边界。实现应至少将空 payload 视为长度/格式错误并返回 `0x13`；仅 SID 的合法性由注册服务自行判断，未注册 SID 返回 `0x11`。
- 原始 task 未明确 UDS event 的字段命名。实现应复用现有 runtime event 结构，至少包含 SID、response type、NRC、connection ID 或 logical address 摘要，不记录完整敏感 payload。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Maybe | 可能需要最小 UDS context 或事件摘要模型；不得实现 ECU 状态机。 |
| Protocols.Uds | Yes | 新增 UDS request/response/NRC 契约、service interface 和 dispatcher。 |
| Protocols.Doip | Yes | 新增或补齐 diagnostic message handler，把 payload 转发给 UDS dispatcher 并封装响应。 |
| Transport | Maybe | 仅在现有 TCP DoIP frame handling 中接入 diagnostic message 转发；不新增 transport 能力。 |
| Host | Yes | 注册 UDS dispatcher 和空服务集合，接入现有依赖注入/运行时。 |
| WebApi | No | 不新增 API；复用 runtime event/recent events 能力。 |
| WebConsole | No | 不新增 UI；复用既有日志视图显示 `uds` 或相关事件。 |
| Tests | Yes | 增加 dispatcher、NRC、DoIP diagnostic forwarding 和 scope check 测试。 |

## Architecture Considerations

- UDS 协议模块应只解析 UDS payload、分发服务并生成 UDS bytes，不直接管理 TCP socket 或 DoIP frame header。
- DoIP diagnostic message handler 应负责 DoIP diagnostic message 与 UDS dispatcher 的边界转换，并保持 DoIP 与 UDS 职责分离。
- Dispatcher 的默认行为必须保守：未注册 SID 返回 `ServiceNotSupported (0x11)`，不得静默丢弃或假装正响应。
- `NegativeResponse` 编码必须固定为 `0x7F, originalSid, nrc`，便于后续服务复用。
- `UdsContext` 只提供后续服务需要的最小上下文形状，不在本 change 实现会话、安全或状态机逻辑。
- 事件发布应复用 task-006/task-007 的 `RuntimeEvent` 管道；本 change 不新增日志 API 或 UI。
- 集成测试应建立本地 TCP Routing Activation 后发送未知 SID，验证响应字节为 `7F xx 11`，并避免依赖外部诊断工具。

## Acceptance Criteria

- [ ] `UdsRequest`、`UdsResponse`、`NegativeResponse` 和基础 NRC 模型已定义。
- [ ] UDS dispatcher 支持注册服务处理器并按 SID 分发。
- [ ] 未注册或未支持 SID 返回 `0x7F SID 0x11`。
- [ ] 请求长度错误或空 UDS payload 返回 `0x7F SID 0x13` 或等价可诊断格式错误响应。
- [ ] DoIP diagnostic message payload 能进入 UDS dispatcher。
- [ ] TCP Routing Activation 后发送未知 UDS SID 可收到 NRC 响应。
- [ ] UDS 请求、响应和错误事件写入现有结构化日志。
- [ ] UDS 请求、响应和错误事件可通过现有 Web 日志 UI 看到。
- [ ] Scope check 确认未实现具体 UDS 正响应服务、ECU 状态机细节或 SecurityAccess。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| UDS dispatcher 被误扩展为具体服务实现 | Medium | High | 在 scope/spec/tasks 中明确只允许注册分发和负响应；scope check 检查无正响应业务服务。 |
| DoIP 与 UDS 职责混杂 | Medium | Medium | DoIP handler 只做 payload 转发和 DoIP response 包装；UDS 模块只处理 UDS 契约和分发。 |
| NRC 编码不一致导致后续服务难复用 | Medium | High | 用单元测试固定 `NegativeResponse` 的 `0x7F SID NRC` bytes。 |
| 空 payload 或格式错误没有稳定错误模型 | Medium | Medium | 明确空 payload 返回 `0x13`，并在单元/集成测试覆盖。 |
| 事件日志暴露完整诊断 payload | Low | Medium | 事件只记录 SID、NRC、响应类型和连接/地址摘要，不要求原始 payload dump。 |
