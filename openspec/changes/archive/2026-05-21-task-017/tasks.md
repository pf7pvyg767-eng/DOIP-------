# Implementation Tasks: Routine、通信控制和 DTC 设置基础服务

**Change ID:** `task-017`
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Phase 1: Routine 配置和 `0x31` MVP

- [x] 1.1 使用并扩展 Routine 配置模型，支持 `routineId` / `identifier`、名称、允许会话、安全要求和 start / stop / requestResults 固定响应。
- [x] 1.2 实现 `0x31` RoutineControl MVP，支持 startRoutine、stopRoutine、requestRoutineResults 三类基础控制类型。
- [x] 1.3 对未知 Routine ID、未支持控制类型、请求长度或格式错误返回明确 NRC。
- [x] 1.4 Routine 调用和拒绝路径发布运行时事件。

**Quality Gate:**
- [x] Routine start / stop / result 固定响应单元测试通过。
- [x] 非法 Routine ID 和格式错误 NRC 测试通过。

---

## Phase 2: `0x28` CommunicationControl 状态

- [x] 2.1 定义通信控制运行时状态，记录当前控制类型、通信类型、最后更新时间和来源。
- [x] 2.2 实现 `0x28` CommunicationControl 基础状态切换服务。
- [x] 2.3 对未支持 controlType、communicationType 或格式错误返回明确 NRC。
- [x] 2.4 状态切换发布运行时事件。
- [x] 2.5 明确保持真实通信通道不关闭、不阻断、不限流。

**Quality Gate:**
- [x] `0x28` 状态切换单元测试通过。
- [x] `0x28` 事件发布测试通过。
- [x] Scope check 确认未修改真实通信通道行为。

---

## Phase 3: `0x85` ControlDTCSetting 状态

- [x] 3.1 定义 DTC 设置运行时状态，记录 enabled / disabled 基础状态和最后请求来源。
- [x] 3.2 实现 `0x85` ControlDTCSetting 基础状态切换服务。
- [x] 3.3 对未支持 settingType 或格式错误返回明确 NRC。
- [x] 3.4 状态切换发布运行时事件。
- [x] 3.5 保持 task-016 DTC runtime store 语义，不实现完整 DTC setting 细分行为。

**Quality Gate:**
- [x] `0x85` 状态切换单元测试通过。
- [x] `0x85` 事件发布测试通过。
- [x] Scope check 确认未实现完整 DTC setting 状态机或存储策略。

---

## Phase 4: Web 展示和集成验证

- [x] 4.1 提供 Routine 配置和控制状态快照读取 API：`GET /api/control-services`。
- [x] 4.2 WebConsole 展示 Routine 配置列表。
- [x] 4.3 WebConsole 展示 CommunicationControl 当前状态。
- [x] 4.4 WebConsole 展示 ControlDTCSetting 当前状态。
- [x] 4.5 集成测试覆盖 `0x31`、`0x28`、`0x85` 响应、状态和事件。

**Quality Gate:**
- [x] `openspec validate task-017 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过。
- [x] 前端变更已执行 `npm run build` 并通过。
- [x] Scope check 确认未实现复杂 Routine 脚本、真实通信通道关闭、完整 DTC setting 细分行为、SecurityAccess 完整流程、Flash、ODX/PDX、PCAP/TLS 或无关重构。

---

## Completion Checklist

- [x] `0x31` RoutineControl 固定响应 MVP 已实现。
- [x] 非法 Routine ID 返回明确 NRC。
- [x] `0x28` CommunicationControl 基础状态切换和事件已实现。
- [x] `0x85` ControlDTCSetting 基础状态切换和事件已实现。
- [x] Web 展示 Routine 配置和控制状态。
- [x] 验收标准全部通过。
- [x] Scope 和 out_of_scope 均已核对。
- [x] 准备进入独立 Test & Status。
