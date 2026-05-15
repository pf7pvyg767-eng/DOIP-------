# Implementation Tasks: 实时事件流 API 和控制台日志视图

**Change ID:** `task-007`

---

## Phase 1: In-Memory Event Buffer

- [x] 1.1 设计并实现内存环形缓冲，用于保存最近 `RuntimeEvent`。
- [x] 1.2 支持按最大容量裁剪旧事件，默认容量可采用 1000 条或等价保守配置。
- [x] 1.3 支持读取最新 N 条事件。
- [x] 1.4 支持按事件分类读取最近事件。
- [x] 1.5 增加缓冲容量、最新 N 条和分类过滤测试。

**Quality Gate:**
- [x] 缓冲不会无限增长。
- [x] 最近事件读取不依赖文件日志或历史搜索。

---

## Phase 2: Event Stream API

- [x] 2.1 在 WebApi 中注册事件流订阅服务，复用既有 `RuntimeEvent` 发布链路。
- [x] 2.2 实现 `WS /api/events/stream` 或等价 WebSocket 端点。
- [x] 2.3 WebSocket 客户端连接后可接收新发布的 `RuntimeEvent` JSON。
- [x] 2.4 WebSocket 客户端断开、取消或异常时释放订阅资源，且服务端继续运行。
- [x] 2.5 实现 `GET /api/events/recent`，支持 `limit` 和 `category` 参数。
- [x] 2.6 增加 API 测试：recent 返回最新 N 条、category 过滤正确。
- [x] 2.7 增加事件流测试：发布事件后客户端可收到消息，断开重连不导致服务异常。

**Quality Gate:**
- [x] `GET /api/events/recent?limit=200&category=doip` 可返回符合条件的最近事件。
- [x] `WS /api/events/stream` 可推送结构化 `RuntimeEvent`。
- [x] 断开和重连路径有测试或明确验证。

---

## Phase 3: WebConsole Event Subscription

- [x] 3.1 增加前端事件 store 或等价状态模块，负责加载最近事件。
- [x] 3.2 前端订阅事件流并追加实时事件。
- [x] 3.3 前端断线后执行简单、有限的重连策略。
- [x] 3.4 前端状态按配置上限裁剪事件，例如最多 1000 条。
- [x] 3.5 页面首次打开时合并最近事件和实时事件，避免明显重复。

**Quality Gate:**
- [x] 页面打开后可看到启动事件。
- [x] 配置保存后日志视图可实时出现配置事件。
- [x] 前端事件状态不会无限增长。

---

## Phase 4: Logs View UI

- [x] 4.1 实现控制台日志列表视图。
- [x] 4.2 日志列表展示时间、等级、分类、事件名称和消息。
- [x] 4.3 实现等级过滤控件。
- [x] 4.4 实现分类过滤控件。
- [x] 4.5 增加加载态、空状态和连接断开/重连状态的基础展示。
- [x] 4.6 增加前端测试或可执行验证：过滤器只显示指定等级/分类事件。

**Quality Gate:**
- [x] 日志列表可扫描运行事件。
- [x] 等级过滤和分类过滤行为可验证。
- [x] UI 未实现复杂图表或历史搜索。

---

## Phase 5: Verification And Scope Check

- [x] 5.1 执行 `openspec validate task-007 --strict`。
- [x] 5.2 执行 `.NET` build 和测试。
- [x] 5.3 如涉及前端构建，执行 WebConsole 对应 npm build/test 命令；若无测试脚本，记录为现有项目约束。
- [x] 5.4 执行 acceptance criteria 核对。
- [x] 5.5 执行 scope check，确认未实现历史日志搜索、复杂图表、100M 吞吐保证或 DoIP/UDS 协议运行时扩展。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] 后端测试通过。
- [x] 前端构建/可用验证通过。
- [x] 验收标准全部满足。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] 内部事件流 API 已提供。
- [x] `WS /api/events/stream` 或等价 WebSocket 端点已实现。
- [x] `GET /api/events/recent` 已实现，并支持 `limit` 和 `category`。
- [x] 前端可订阅事件流。
- [x] 日志列表已实现。
- [x] 等级过滤已实现。
- [x] 分类过滤已实现。
- [x] 内存环形缓冲已实现。
- [x] UI 最多保留配置数量的事件，例如 1000 条。
- [x] 页面打开后能看到启动事件。
- [x] 新配置保存后日志视图实时出现配置事件。
- [x] 断开后重连不会导致服务异常。
- [x] 未实现历史日志搜索。
- [x] 未实现复杂图表。
- [x] 未承诺或实现 100M 吞吐保证。
- [x] 未扩展 DoIP/UDS 协议运行时行为。
- [x] OpenSpec 严格校验已执行。
