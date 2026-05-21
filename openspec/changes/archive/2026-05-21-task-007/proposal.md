# Proposal: 实时事件流 API 和控制台日志视图

**Change ID:** `task-007`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

当前系统已具备结构化 `RuntimeEvent`、事件发布接口、UTF-8 文件日志，以及基础 Web 控制台页面，但控制台还不能实时观察运行事件。用户打开控制台后无法看到启动事件，也无法在配置保存后立即看到配置事件，导致运行状态、配置变更和早期诊断仍然缺少闭环。

本 change 需要在不扩展 DoIP/UDS 协议行为、不实现历史日志搜索或复杂图表的前提下，提供内部实时事件流 API、最近事件读取能力、前端日志视图和有限内存缓冲，避免 UI 事件列表无限增长。

## Proposed Solution

- 在 WebApi 中提供内部事件流 API，建议包括 `WS /api/events/stream` 和 `GET /api/events/recent?limit=200&category=doip`。
- 基于既有 `RuntimeEvent` 发布链路增加内存环形缓冲，保存最近事件供页面首次加载和重连后补齐。
- 前端控制台订阅事件流，实时接收 `RuntimeEvent` JSON。
- 前端实现日志列表，显示事件时间、等级、分类、名称和消息等核心字段。
- 前端实现等级过滤和分类过滤。
- 前端事件列表按配置上限保留，例如默认最多 1000 条，避免 UI 无限增长。
- 断线或重连失败时前端应保持页面可用，服务端断开连接后不得异常崩溃。

## Scope

### In Scope

- 提供内部事件流 API。
- 支持 `WS /api/events/stream` 或等价 WebSocket 事件流端点。
- 支持最近事件读取 API，建议为 `GET /api/events/recent?limit=200&category=doip`。
- WebApi 将已发布的 `RuntimeEvent` 推送给订阅客户端。
- 实现服务端内存环形缓冲，保存最近运行事件。
- 前端订阅事件流并追加实时事件。
- 前端首次加载或重连时读取最近事件。
- 前端实现日志列表。
- 前端实现等级过滤。
- 前端实现分类过滤。
- 前端最多保留配置数量的事件，例如 1000 条。
- 增加聚焦测试，覆盖最近事件 API、WebSocket 推送、断开重连稳定性和前端过滤行为。

### Out of Scope

- 不实现历史日志搜索。
- 不实现复杂图表。
- 不保证 100M 吞吐。
- 不实现跨进程或持久化事件回放。
- 不实现日志索引、全文检索或分页搜索。
- 不实现鉴权、租户隔离或外部观察平台接入。
- 不扩展 DoIP/UDS 协议运行时行为。

## Open Questions

- 任务未指定事件流端点是否必须使用原生 WebSocket、中间件还是 SignalR；实现应优先选择当前 WebApi 依赖中最小可用的 WebSocket 方案，除非现有项目已有实时通信约定。
- 任务未指定最近事件 API 的默认 `limit` 和最大 `limit`。实现应采用保守默认值，例如 200，并设置上限避免请求过大。
- 任务未指定 UI 事件保留上限的配置来源。实现应采用明确常量或最小配置项，默认 1000 条；后续 task 可再扩展为用户可配置。
- 任务未指定断线后的重连策略。实现应采用简单、有限、不会造成服务端压力的重连策略，并确保断开后服务端资源可释放。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | No | 复用 task-006 已定义的 `RuntimeEvent`、等级和分类契约。 |
| Observability | Yes | 增加内存环形缓冲或等价事件缓存，用于最近事件读取和前端初始化。 |
| WebApi | Yes | 增加事件流端点和最近事件读取端点，接入既有事件发布链路。 |
| WebConsole | Yes | 增加日志视图、事件订阅、最近事件加载、等级过滤和分类过滤。 |
| Tests | Yes | 增加 API、WebSocket 和前端过滤相关聚焦测试。 |
| Protocol logic | No | 不新增或修改 DoIP/UDS 协议运行时行为。 |

## Architecture Considerations

- 事件流 API 应复用 task-006 的 `RuntimeEvent` 契约，避免为 Web 推送定义第二套事件模型。
- 内存环形缓冲应位于 WebApi/Observability 边界，作为短期运行态缓存；不得读取或搜索历史日志文件。
- `GET /api/events/recent` 应只返回内存缓冲中的最近事件，并支持 `limit`、`category` 等基础过滤。
- WebSocket 客户端断开应被视为正常情况，服务端应释放订阅并继续运行。
- 前端 UI 保留上限应在追加实时事件和加载最近事件时同时生效，避免长时间运行后 DOM 和状态无限增长。
- 等级过滤和分类过滤应在前端状态中完成；后端最近事件 API 可支持基础 category 过滤以减少初始化数据量。
- 本 task 不引入复杂图表、高吞吐优化或协议行为变更；事件吞吐只需满足早期控制台可观察闭环。

## Acceptance Criteria

- [x] 页面打开后能看到启动事件。
- [x] 新配置保存后日志视图实时出现配置事件。
- [x] 断开后重连不会导致服务异常。
- [x] UI 最多保留配置数量的事件，例如 1000 条。
- [x] `GET /api/events/recent` 可返回最新 N 条内存事件。
- [x] `GET /api/events/recent?limit=200&category=doip` 可按分类返回最近事件。
- [x] `WS /api/events/stream` 可向已连接客户端推送新发布的 `RuntimeEvent`。
- [x] 日志列表支持等级过滤。
- [x] 日志列表支持分类过滤。
- [x] Scope check 确认未实现历史日志搜索、复杂图表、100M 吞吐保证或 DoIP/UDS 协议扩展。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 事件流 API 范围扩大为历史日志搜索 | Medium | Medium | 明确最近事件只来自内存环形缓冲，不读取或索引文件日志。 |
| WebSocket 断开未释放订阅 | Medium | High | 断开、异常和取消路径都清理订阅，并增加断开重连测试。 |
| 前端长时间运行导致状态无限增长 | Medium | Medium | 在 store 或视图层统一执行最大保留数量裁剪。 |
| 过滤逻辑与后端分类不一致 | Medium | Low | 复用 `RuntimeEvent` 的 `level` 和 `category` 字段，测试覆盖等级/分类过滤。 |
| 实现误改协议运行时行为 | Low | High | 将改动限制在 Observability/WebApi/WebConsole 和测试，scope check 明确排除 DoIP/UDS 行为扩展。 |
