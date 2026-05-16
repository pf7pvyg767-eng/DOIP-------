# Implementation Tasks: 最小 ECU 状态和 `0x10`/`0x3E`

**Change ID:** `task-012`
**Status:** Implementation Complete

---

## Phase 1: 最小 ECU Runtime State

- [x] 1.1 定义 `SessionState` 或等价 session 模型，覆盖默认、扩展和编程会话。
- [x] 1.2 定义最小 `EcuRuntimeState`，包含 logical address、当前 session、security locked 摘要和最近 TesterPresent 时间。
- [x] 1.3 提供线程安全或 Host 生命周期内可共享的 state 访问方式，供 UDS 服务读取和更新。
- [x] 1.4 初始化默认 session 为默认会话，security 状态保持 locked 摘要，不实现解锁流程。
- [x] 1.5 增加 runtime state 单元测试，覆盖初始状态、session 更新和 TesterPresent 时间更新。

**Quality Gate:**
- [x] ECU state 仅包含本 task 需要的最小字段。
- [x] 未实现 SecurityAccess、完整状态机或持久化迁移。

---

## Phase 2: `0x10` DiagnosticSessionControl

- [x] 2.1 实现 `DiagnosticSessionControlService` 并按 SID `0x10` 注册到现有 UDS dispatcher。
- [x] 2.2 支持 `10 01` 切换默认会话。
- [x] 2.3 支持 `10 03` 切换扩展会话。
- [x] 2.4 支持 `10 02` 切换编程会话。
- [x] 2.5 正响应编码为 `0x50, subFunction, P2, P2*` 或项目内等价固定格式，并包含基础 P2/P2* 参数。
- [x] 2.6 对未知或不支持子功能返回明确 NRC，复用现有 negative response 模型。
- [x] 2.7 会话变化时发布 runtime event，包含旧 session、新 session 和连接或 logical address 摘要。
- [x] 2.8 增加 `10 01`、`10 03`、`10 02`、未知子功能和 P2/P2* 响应参数测试。

**Quality Gate:**
- [x] `0x10` 只处理 session control，不实现其他 UDS 服务。
- [x] P2/P2* 仅作为响应参数返回，不实现定时器或 ResponsePending。

---

## Phase 3: `0x3E` TesterPresent

- [x] 3.1 实现 `TesterPresentService` 并按 SID `0x3E` 注册到现有 UDS dispatcher。
- [x] 3.2 支持 `3E 00` 返回正响应 `0x7E, 0x00`。
- [x] 3.3 收到 `3E 00` 时更新最近 TesterPresent 时间或等价最小 state。
- [x] 3.4 对未知或不支持子功能返回明确 NRC，复用现有 negative response 模型。
- [x] 3.5 增加 `3E 00` 正响应、最近 TesterPresent 时间更新和未知子功能测试。

**Quality Gate:**
- [x] 不实现 TesterPresent 超时回退。
- [x] 不实现 session timeout、P2/P2* timeout 或 ResponsePending。

---

## Phase 4: 集成、事件和范围验证

- [x] 4.1 在 Host 依赖注入或 UDS 服务注册路径中注册最小 ECU state、`0x10` 服务和 `0x3E` 服务。
- [x] 4.2 增加 Routing Activation 后发送 `10 01` 的集成测试，验证默认会话切换和正响应。
- [x] 4.3 增加 Routing Activation 后发送 `10 03` 的集成测试，验证扩展会话切换和正响应。
- [x] 4.4 增加 Routing Activation 后发送 `10 02` 的集成测试，验证编程会话切换和正响应。
- [x] 4.5 增加 Routing Activation 后发送 `3E 00` 的集成测试，验证正响应。
- [x] 4.6 增加会话变化事件可见性测试，确认事件进入现有结构化事件管道。
- [x] 4.7 运行 `openspec validate task-012 --strict`。
- [x] 4.8 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 4.9 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 4.10 若前端文件未变更，记录不需要运行 npm。
- [x] 4.11 执行 scope check：未实现 TesterPresent 超时回退、SecurityAccess、ResponsePending、DID/DTC/Routine、刷写或新增 Web UI。

**Quality Gate:**
- [x] 核心验证、验收标准和 scope check 均通过。
- [x] `dotnet format` 如执行，必须作为带超时的非阻塞可选检查。

---

## Acceptance Checklist

- [x] `10 01` 切换默认会话。
- [x] `10 03` 切换扩展会话。
- [x] `10 02` 切换编程会话。
- [x] `0x10` 正响应返回基础 P2/P2* 参数。
- [x] `3E 00` 返回正响应。
- [x] `3E 00` 更新最近 TesterPresent 时间或等价 state。
- [x] 会话变化可以在事件中看到。
- [x] 未实现 TesterPresent 超时回退。
- [x] 未实现 SecurityAccess。
- [x] 未实现 ResponsePending。
- [x] 未实现 DID、DTC、Routine 或刷写服务。
