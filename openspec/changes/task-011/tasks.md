# Implementation Tasks: UDS 分发框架和 NRC 响应模型

**Change ID:** `task-011`
**Status:** Implementation Complete
**Completed:** 2026-05-16

---

## Phase 1: UDS 契约和 NRC 模型

- [x] 1.1 定义 `UdsRequest`，包含 `ServiceId`、原始 SID 和请求 payload bytes。
- [x] 1.2 定义 `UdsResponse` 抽象响应契约，并提供统一 `ToBytes()` 编码入口。
- [x] 1.3 定义 `NegativeResponse`，编码格式固定为 `0x7F, originalSid, nrc`。
- [x] 1.4 定义基础 NRC：`ServiceNotSupported (0x11)` 和 `IncorrectMessageLengthOrInvalidFormat (0x13)`。
- [x] 1.5 定义最小 `UdsContext`，仅承载 connection ID、远端端点和 tester/ECU logical address 摘要，不实现 ECU 状态机。
- [x] 1.6 增加 NRC 和 response bytes 编码单元测试。

**Quality Gate:** PASSED

---

## Phase 2: UDS Dispatcher 和服务注册

- [x] 2.1 定义 `IUdsService` 接口，包含 `ServiceId` 和异步处理方法。
- [x] 2.2 实现 `UdsDispatcher`，支持注册一个或多个服务处理器。
- [x] 2.3 Dispatcher 收到已注册 SID 时调用对应处理器，并按返回顺序返回响应。
- [x] 2.4 Dispatcher 收到未注册 SID 时返回 `NegativeResponse(ServiceNotSupported)`。
- [x] 2.5 Dispatcher 收到空 payload 或无法形成有效 `UdsRequest` 的输入时返回 `IncorrectMessageLengthOrInvalidFormat`。
- [x] 2.6 增加未知服务返回 `7F xx 11` 单元测试。
- [x] 2.7 增加服务处理器注册和分发单元测试。
- [x] 2.8 增加长度/格式错误返回 `0x13` 单元测试。

**Quality Gate:** PASSED

---

## Phase 3: DoIP Diagnostic Message 转发

- [x] 3.1 在 TCP DoIP frame 处理链路中识别 `DiagnosticMessage (0x8001)` payload。
- [x] 3.2 Routing Activation 已完成的连接收到 diagnostic message 时，将 UDS payload 转发给 dispatcher。
- [x] 3.3 将 dispatcher 返回的 UDS response bytes 包装为 DoIP diagnostic message response，并写回同一 TCP 连接。
- [x] 3.4 空 UDS payload 返回基础 NRC，不崩溃 TCP 连接。
- [x] 3.5 保持未完成 Routing Activation 的连接行为在 task-010 既有边界内，不引入 UDS 正响应流程。
- [x] 3.6 增加 TCP Routing Activation 后发送未知 UDS SID 收到 `7F SID 11` 的集成测试。

**Quality Gate:** PASSED

---

## Phase 4: Runtime Events、验证和范围检查

- [x] 4.1 发布 UDS request 摘要事件，包含 SID 和连接/地址摘要。
- [x] 4.2 发布 UDS response 摘要事件，包含 response type、SID 和 NRC 摘要。
- [x] 4.3 发布 UDS 格式错误和 unsupported SID 事件，不记录完整敏感 payload。
- [x] 4.4 验证现有结构化日志可记录 UDS request/response/error 事件。
- [x] 4.5 验证现有 Web 日志 UI 可通过 runtime event pipeline 看到 `uds` 事件，无需新增页面。
- [x] 4.6 运行 `openspec validate task-011 --strict`。
- [x] 4.7 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 4.8 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 4.9 前端文件未变更，记录不需要 npm。
- [x] 4.10 执行 scope check：未实现具体 UDS 正响应服务、ECU 状态机细节、SecurityAccess、ResponsePending、手动 NRC 或新增 Web UI。

**Quality Gate:** PASSED

---

## Acceptance Checklist

- [x] `UdsRequest`、`UdsResponse`、`NegativeResponse` 和基础 NRC 已定义。
- [x] UDS dispatcher 支持服务注册和按 SID 分发。
- [x] 未支持 SID 返回 `0x7F SID 0x11`。
- [x] 请求长度错误返回 `0x13`。
- [x] DoIP diagnostic message payload 能进入 UDS dispatcher。
- [x] TCP Routing Activation 后发送未知 UDS 服务可收到 NRC。
- [x] UDS 请求事件写入结构化日志。
- [x] UDS 响应事件写入结构化日志。
- [x] UDS 事件可通过现有 Web 日志 UI 看到。
- [x] 未实现具体 UDS 服务正响应。
- [x] 未实现 ECU 状态机细节。
- [x] 未实现 SecurityAccess。

## Out of Scope Checklist

- [x] 不实现 `0x10`、`0x3E`、DID、DTC、Routine、刷写或其他正响应业务服务。
- [x] 不实现 Session 状态迁移、P2/P2* 计时或 TesterPresent 超时。
- [x] 不实现 SecurityAccess。
- [x] 不实现 `0x78 ResponsePending` 策略。
- [x] 不实现手动 NRC、异常注入或自定义 UDS 响应。
- [x] 不新增 Web UI 页面、复杂图表或 UDS 管理界面。
- [x] 不改变 TCP Routing Activation、UDP discovery、TLS、PCAP 或外部 observability 范围。
