# Proposal: Routine、通信控制和 DTC 设置基础服务

**Change ID:** `task-017`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

当前模拟器已有 UDS dispatcher、会话运行时、DTC MVP、事件日志和 Web 观察能力，但常见控制类 UDS 服务仍缺少最小闭环。诊断调试人员需要在不引入完整 ECU 行为的前提下，通过 `0x31` 调用已配置 Routine 并拿到固定响应，通过 `0x28` 和 `0x85` 切换模拟器内部控制状态，并在 Web 侧看到 Routine 配置和当前控制状态。

本 change 只覆盖 MVP 基础路径：RoutineControl 固定响应、CommunicationControl 状态切换、ControlDTCSetting 状态切换，以及 Web 展示。不实现复杂 Routine 脚本、真实通信通道关闭或完整 DTC setting 细分行为。

## Proposed Solution

- 扩展 Routine 配置结构，支持按 Routine ID 配置名称、允许会话、安全要求和 `start` / `stop` / `requestResults` 固定响应。
- 新增 UDS `0x31` RoutineControl MVP 服务，处理三类基础控制类型，并对未知 Routine ID、未支持控制类型、格式错误返回明确 NRC。
- 新增通信控制运行时状态，记录 `0x28` 的最近请求、控制类型和通信类型，并发布状态变化事件；不实际关闭 TCP/UDP/DoIP 通道。
- 新增 DTC 设置运行时状态，记录 `0x85` 的 enabled / disabled 状态，并发布状态变化事件；不实现 DTC status 位或存储细分行为。
- 在 Web API 和 WebConsole 中展示 Routine 配置和控制状态快照。
- 增加单元测试、API 测试和 scope check。

## Scope

### In Scope

- `0x31` RoutineControl 固定响应 MVP。
- `0x28` CommunicationControl 基础状态切换。
- `0x85` ControlDTCSetting 基础状态切换。
- Web 展示 Routine 配置和控制状态。
- Routine、通信控制、DTC 设置相关运行时事件和日志。
- 针对 Routine start / stop / result、非法 Routine ID、状态切换和事件发布的测试。

### Out of Scope

- 不实现复杂 Routine 执行脚本。
- 不实现真实通信通道关闭、阻断或网络栈级别限流。
- 不实现完整 DTC setting 细分行为、DTC 状态机或 DTC 存储策略。
- 不扩大到 SecurityAccess 完整流程、Flash、ODX/PDX、PCAP/TLS 或其他诊断流程。
- 不新增 Routine 持久化编辑、动态脚本引擎、外部文件导入或无关重构。

## Acceptance Criteria

- [x] 配置内 Routine 可通过 `0x31` 调用并返回固定响应。
- [x] 非法 Routine ID 返回明确 NRC。
- [x] `0x28` 改变通信控制状态并产生事件。
- [x] `0x85` 改变 DTC 设置状态并产生事件。
- [x] Web 能展示 Routine 配置和通信控制 / DTC 设置当前状态。
- [x] Scope check 确认未实现复杂 Routine 脚本、真实通信通道关闭、完整 DTC setting 细分行为、SecurityAccess 完整流程、Flash、ODX/PDX、PCAP/TLS 或无关诊断流程。
