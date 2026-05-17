# Implementation Tasks: 连接、DoIP、UDS 实时观察 UI

**Change ID:** `task-014`
**Status:** Implementation Complete
**完成日期:** 2026-05-17

---

## Phase 1: 后端快照 API 与事件数据

- [x] 1.1 新增或扩展连接快照 DTO，覆盖连接 ID、传输类型、远端端点、Routing Activation、逻辑地址、连接时间和连接状态。
- [x] 1.2 实现 `GET /api/connections`，返回当前连接快照列表。
- [x] 1.3 新增 ECU 状态快照 DTO，覆盖 ECU 逻辑地址、当前诊断会话、安全状态摘要和最近 TesterPresent 时间。
- [x] 1.4 实现 `GET /api/ecu/state`，返回当前 ECU 状态快照。
- [x] 1.5 确保连接打开/关闭、DoIP frame 收发、UDS 请求/响应、会话切换事件通过现有 `RuntimeEvent` 管道提供 UI 所需摘要数据。

**Quality Gate:**
- [x] API 测试覆盖 `GET /api/connections`。
- [x] API 测试覆盖 `GET /api/ecu/state`。
- [x] 事件测试确认所需事件名称和关键字段可被 UI 消费。

---

## Phase 2: 前端数据模型与实时更新

- [x] 2.1 在 WebConsole API 层新增 `ConnectionSnapshot`、`EcuStateSnapshot`、DoIP trace item、UDS trace item 等类型。
- [x] 2.2 新增加载函数调用 `GET /api/connections` 和 `GET /api/ecu/state`。
- [x] 2.3 复用现有 RuntimeEvent WebSocket，按事件名称更新连接列表、DoIP 报文列表、UDS 报文列表和 ECU 状态。
- [x] 2.4 实现 WebSocket 断线重连后的快照刷新，避免 UI 长期停留在旧状态。
- [x] 2.5 使用有界列表保存报文记录，避免前端内存无限增长。

**Quality Gate:**
- [x] 客户端连接事件会新增或更新连接记录。
- [x] 连接关闭事件会把对应连接标记为关闭。
- [x] `state.session.changed` 事件会更新 ECU 状态。

---

## Phase 3: 诊断观察 UI

- [x] 3.1 新增诊断观察视图区域，作为控制台的主要诊断观察页面之一。
- [x] 3.2 新增连接表展示连接列表和连接状态。
- [x] 3.3 新增通用报文表，分别用于 DoIP 报文列表和 UDS 报文列表。
- [x] 3.4 新增 ECU 状态面板展示 ECU 当前状态。
- [x] 3.5 实现基础过滤控件，支持按连接、方向、事件类别/名称或关键字过滤。
- [x] 3.6 保持现有事件日志视图可用，未删除既有基础 dashboard 能力。

**Quality Gate:**
- [x] 前端渲染由 `npm run build` 的 TypeScript/Vite 构建覆盖。
- [x] 前端过滤覆盖连接过滤和报文关键字过滤逻辑。
- [x] UI 表格和状态面板使用固定网格与横向滚动约束，避免关键文本溢出破坏布局。

---

## Phase 4: 验收与 Scope Check

- [x] 4.1 增加后端/API 与事件覆盖：客户端连接后 UI/数据模型可显示连接。
- [x] 4.2 增加后端/API 与事件覆盖：发送 DoIP/UDS 请求后 UI/数据模型可显示请求和响应。
- [x] 4.3 增加后端/API 与事件覆盖：会话切换后 UI/数据模型实时更新 ECU 状态。
- [x] 4.4 增加后端/API 与事件覆盖：断开连接后 UI/数据模型显示连接关闭。
- [x] 4.5 执行 scope check，确认未实现报文重放、图表分析或 pcap 下载。
- [x] 4.6 运行 OpenSpec、后端和前端相关验证命令。

**Quality Gate:**
- [x] `openspec validate task-014 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过。
- [x] 前端 `npm run build` 通过。
- [x] 未执行 `dotnet format`；该项仍按规则作为带明确超时的非阻塞可选检查。

---

## Completion Checklist

- [x] 已生成 `GET /api/connections` 和 `GET /api/ecu/state` 的只读实现。
- [x] 连接列表 UI 已实现并实时响应连接打开/关闭。
- [x] DoIP 报文列表 UI 已实现并显示收发方向。
- [x] UDS 报文列表 UI 已实现并显示请求/响应。
- [x] ECU 状态面板已实现并在会话切换后实时更新。
- [x] 基础过滤已实现。
- [x] 验收标准全部通过。
- [x] Out of scope 项均未实现。
- [x] 准备进入独立 Test & Status。

## 执行记录

- 2026-05-17：完成后端快照 API、RuntimeEvent 摘要字段、WebConsole 实时观察 UI 和相关测试。
- 2026-05-17：`dotnet build .\DoipSimulator.sln -m:1` 首次发现 `DoipPayloadType.Name` 编译错误，已改为使用既有 `KnownName`。
- 2026-05-17：`dotnet test .\DoipSimulator.sln -m:1` 首次出现 `HostRunWritesStartupAndStopEventsToRuntimeLog` 启停日志等待超时；单测复跑通过，完整测试复跑通过，记录为偶发启动时序问题。
- 2026-05-17：前端首次 `npm run build` 因未安装依赖失败；执行 `npm ci` 后，沙箱内 Vite/esbuild 子进程 `spawn EPERM`，已在沙箱外复跑并通过。
