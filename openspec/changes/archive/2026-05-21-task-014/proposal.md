# Proposal: 连接、DoIP、UDS 实时观察 UI

**Change ID:** `task-014`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

当前控制台已经具备基础仪表盘和运行时事件流，但还不能面向诊断调试人员集中展示 TCP 连接、DoIP 报文、UDS 请求/响应和 ECU 会话状态。`task-010`、`task-012`、`task-013` 已提供连接管理、Routing Activation、会话状态和 DID 读取能力，`task-014` 需要把这些运行时信息以 Web UI 和最小快照 API 暴露出来，方便实时观察诊断交互。

## Proposed Solution

- 在 WebApi 增加只读快照 API：`GET /api/connections` 和 `GET /api/ecu/state`。
- 复用现有 `RuntimeEvent` WebSocket 流，不新增独立实时协议。
- 在 WebConsole 增加诊断观察视图，包含连接列表、DoIP 报文列表、UDS 报文列表和 ECU 状态面板。
- UI 初次加载时通过 API 获取连接和 ECU 状态快照，并通过 WebSocket 订阅后续事件增量更新。
- UI 关注 `connection.opened`、`connection.closed`、`doip.frame.received`、`doip.frame.sent`、`uds.request.received`、`uds.response.sent`、`state.session.changed` 等事件。
- 基础过滤支持按连接、方向、报文类型/事件名称或关键字缩小连接和报文列表，不实现复杂查询语言。

## Scope

### In Scope

- 连接列表 UI，展示连接 ID、传输类型、远端端点、Routing Activation 状态、逻辑地址和连接状态。
- DoIP 报文列表 UI，展示接收和发送的 DoIP frame 摘要。
- UDS 报文列表 UI，展示 UDS 请求和响应摘要。
- ECU 状态面板，展示逻辑地址、当前诊断会话、安全状态摘要和 TesterPresent 最近时间等可用状态。
- 基础过滤，覆盖连接、DoIP 报文和 UDS 报文的常用筛选。
- `GET /api/connections` 返回当前连接快照。
- `GET /api/ecu/state` 返回当前 ECU runtime state 快照。
- 复用 `RuntimeEvent` WebSocket 流驱动 UI 实时更新。
- 对连接打开、连接关闭、DoIP 收发、UDS 请求/响应、会话切换进行测试覆盖。

### Out of Scope

- 不实现报文重放。
- 不实现图表分析。
- 不实现 pcap 下载。
- 不新增 pcap 录制能力。
- 不新增 DoIP/UDS 协议行为或诊断服务。
- 不实现 DID/DTC/Routine/Flash/SecurityAccess 管理界面。
- 不实现多用户权限、持久化历史查询或数据库存储。

## Implementation Summary

- WebApi 已新增 `GET /api/connections` 和 `GET /api/ecu/state`。
- Host 已将同一份 `ConnectionRegistry` 和 `EcuRuntimeState` 注入 TCP runtime 与 WebApi，保证快照 API 反映当前运行时状态。
- RuntimeEvent 已补充 UI 所需事件和摘要字段：连接打开/关闭、DoIP frame 收发、UDS 请求/响应字节摘要、`state.session.changed`。
- WebConsole 已新增实时观察区域，包含 ECU 状态、连接列表、DoIP 列表、UDS 列表和基础过滤。
- 现有事件日志和基础 dashboard 保持可用。

## Acceptance Criteria

- [x] 客户端连接后 UI 显示该连接。
- [x] 发送 DoIP/UDS 请求后 UI 显示请求和响应。
- [x] 会话切换后 UI 状态实时更新。
- [x] 断开连接后 UI 显示连接关闭。
- [x] `GET /api/connections` 返回当前连接快照。
- [x] `GET /api/ecu/state` 返回当前 ECU 状态快照。
- [x] Scope check 确认未实现报文重放、图表分析或 pcap 下载。

## Verification

- `openspec validate task-014 --strict`：通过。
- `dotnet build .\DoipSimulator.sln -m:1`：通过；仅有非阻塞 `NU1900` NuGet vulnerability feed 访问警告。
- `dotnet test .\DoipSimulator.sln -m:1`：通过；92 passed, 0 failed, 0 skipped；仅有非阻塞 `NU1900` 警告。
- `npm run build`：通过；首次沙箱内运行因 Vite/esbuild 子进程 `spawn EPERM` 失败，沙箱外复跑通过。
- `dotnet format`：未执行，按任务规则仍为带明确超时的非阻塞可选检查。
