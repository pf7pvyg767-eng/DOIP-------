# Proposal: Vue 控制台基础页面

**Change ID:** `task-005`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

`task-002` 已提供 WebApi 启动入口和 `GET /api/health`，`task-004` 已提供 `GET /api/config`。当前 WebConsole 仍缺少可打开的基础控制台页面，无法让用户在浏览器中直接确认服务健康状态、启动时间、版本和关键配置摘要。

需要在不引入编辑能力和运行时观测复杂功能的前提下，提供一个最小但可用的 Vue 控制台首页，消费现有后端 API 并展示当前状态。

## Proposed Solution

新增 Vue 基础控制台页面能力：

- 在 WebConsole 中实现基础页面布局和 Dashboard 视图。
- 通过前端 API client 读取 `GET /api/health` 和 `GET /api/config`。
- 展示服务状态、启动时间、版本。
- 从当前配置中提取并展示 VIN、EID、GID、逻辑地址和 DoIP 端口摘要。
- 为数据加载中和后端不可用提供清晰页面状态。
- 增加聚焦的前端单元测试和 API mock 测试，覆盖成功、加载和失败渲染。

## Scope

### In Scope

- 实现可打开的 Vue WebConsole 基础页面。
- 实现 Dashboard 页面布局。
- 实现前端 API client，消费 `GET /api/health` 与 `GET /api/config`。
- 展示服务状态、启动时间、版本。
- 展示配置摘要：VIN、EID、GID、逻辑地址、DoIP UDP 端口、DoIP TCP 端口、DoIP TLS 端口。
- 提供基础加载态。
- 提供后端不可用或请求失败时的错误态。
- 页面刷新后重新读取后端数据并保持展示正确。
- 添加聚焦的前端测试，覆盖状态组件渲染、API mock 成功状态和失败状态。

### Out of Scope

- 不实现配置编辑。
- 不实现配置保存、局部更新或表单校验。
- 不实现日志流。
- 不实现协议报文列表。
- 不实现连接、DoIP 或 UDS 实时观测视图。
- 不新增或修改后端 API 契约。
- 不引入权限系统、认证或授权。
- 不扩大到 DoIP/UDS 运行时行为。

## Open Questions

- 任务未指定最终视觉设计规范。实现应采用现有 WebConsole/Vite/Vue 骨架中的最小一致样式，避免引入完整设计系统。
- 任务未指定 WebConsole 与 WebApi 的部署拓扑。实现应采用现有 Vite/Host 约定中最小可运行的 API base URL 或代理方式，保证启动 URL 可访问控制台。
- 任务未明确 `GET /api/health` 的完整响应字段名称。实现应基于现有 health 响应读取服务状态、启动时间和版本；若字段缺失，应提供合理占位而不扩大后端范围。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| WebConsole | Yes | 实现 App、Dashboard、StatusPanel、API client 和相关样式。 |
| WebApi | No | 仅消费既有 `GET /api/health` 与 `GET /api/config`，不修改 API。 |
| Core | No | 不修改配置模型或校验逻辑。 |
| Host | Maybe | 仅在现有启动 URL/静态文件接线需要时做最小接入，目标是能打开控制台，不改变运行时行为。 |
| Tests | Yes | 添加前端组件/API mock 测试，覆盖成功、加载和错误状态。 |
| Protocol logic | No | 不新增 DoIP/UDS、日志流或协议报文观测能力。 |

## Architecture Considerations

- 前端应通过单一 API client 读取 health 和 config，避免组件内散落 fetch 细节。
- Dashboard 状态应至少区分 `loading`、`ready` 和 `error`，刷新页面时从后端重新获取数据。
- 配置摘要应从完整 `SimulatorConfig` 投影为只读 `configSummary`，不暴露编辑入口。
- 错误态应面向后端不可用、网络错误或 API 非成功响应，页面应保持可渲染。
- UI 实现应保持基础控制台范围，不提前加入日志流、协议报文列表、配置编辑表单或实时观测导航。

## Acceptance Criteria

- [ ] 打开启动 URL 能看到 Web 控制台。
- [ ] 控制台正确展示服务状态、启动时间和版本。
- [ ] 控制台正确展示 VIN、EID、GID、逻辑地址和端口配置摘要。
- [ ] 后端不可用或 API 请求失败时，页面显示错误状态。
- [ ] 页面刷新后重新加载数据，服务状态和配置摘要仍正确。
- [ ] 加载过程中页面显示基础加载态。
- [ ] Scope check 确认未实现配置编辑、日志流、协议报文列表或 DoIP/UDS 运行时行为。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 基础控制台扩展为配置编辑或运行时观测页面 | Medium | Medium | 仅展示只读状态与配置摘要，不加入表单、保存按钮、日志或报文列表。 |
| 前端 API 字段假设与后端响应不一致 | Medium | Medium | 基于现有 API 响应和测试 mock 定义最小类型，缺失非关键字段时显示占位。 |
| WebConsole 启动 URL 与 WebApi API base URL 接线不清晰 | Medium | Medium | 采用现有 Vite/Host 约定中的最小代理或相对路径方案，并用手工访问验证。 |
| 后端不可用时页面崩溃 | Low | Medium | 统一捕获请求失败并渲染错误态，测试覆盖失败状态。 |
