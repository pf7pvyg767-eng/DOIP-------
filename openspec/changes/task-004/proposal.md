# Proposal: 配置读取与保存 API

**Change ID:** `task-004`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

`task-002` 已建立 WebApi 运行入口，`task-003` 已建立 `SimulatorConfig`、JSON 读取/保存与字段级校验能力，但当前 WebApi 尚未提供配置读取与保存接口。后续 Web 控制台配置闭环需要通过 API 读取当前配置、提交完整配置、接收字段级校验错误，并在保存成功后通知应用内组件配置已变化。

## Proposed Solution

新增受限的配置 API 能力：

- 在 WebApi 中提供 `GET /api/config`，返回当前 `SimulatorConfig`。
- 在 WebApi 中提供 `PUT /api/config`，接收完整 `SimulatorConfig` 并保存合法配置。
- 复用 `task-003` 的配置校验能力，将非法配置转换为 HTTP `400` 和字段级错误响应。
- 保存成功后发出配置变更事件，供后续运行时或控制台能力订阅。
- 通过测试覆盖读取、保存、非法配置错误和保存后重启/重建配置存储可加载新配置。

## Scope

### In Scope

- 增加配置读取 API：`GET /api/config`。
- 增加配置保存 API：`PUT /api/config`。
- `GET /api/config` 返回当前 `SimulatorConfig`。
- `PUT /api/config` 支持保存完整 `SimulatorConfig`。
- 非法配置返回 HTTP `400` 和字段级校验错误。
- 保存成功后发出配置变更事件。
- 保存后重启服务或重建 `ConfigStore` 能加载新配置。
- 添加聚焦的 WebApi/API 测试和必要的事件契约测试。

### Out of Scope

- 不支持局部 `PATCH`。
- 不支持运行中热应用复杂协议变更。
- 不做权限系统、认证或授权。
- 不实现 UI。
- 不扩大到 DoIP/UDS 运行时行为。
- 不实现 DID、DTC、Routine、Session、SecurityAccess、Flash 或 TLS 协议行为。
- 不引入 YAML、ODX/PDX 导入或配置版本迁移。

## Open Questions

- 任务未指定配置文件路径如何从 Host 传入 WebApi。实现应采用现有项目中最小、可测试的配置路径注入方式，不扩大为完整配置管理系统。
- 任务未指定配置变更事件的具体总线或接口形态。实现应提供最小事件契约，满足保存成功后可观察到事件，且不触发复杂运行时热应用。
- 任务给出了错误响应示例，但未指定所有错误字段名称的最终 JSON casing。实现应保持字段级 `path` 和 `message` 清晰可断言，并尽量贴近示例。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| WebApi | Yes | 增加 `GET /api/config` 与 `PUT /api/config` 路由或控制器，映射成功与校验失败响应。 |
| Core | Maybe | 可增加最小配置变更事件契约；复用现有 `SimulatorConfig`、`ConfigStore` 和 `ConfigValidator`。 |
| Host | Maybe | 如 WebApi 需要配置文件路径或依赖注入，Host 仅做必要接线，不改变运行时协议行为。 |
| WebConsole | No | 不实现 UI 或配置编辑页面。 |
| Tests | Yes | 增加 API 测试，覆盖读取、保存、非法配置、保存后重载和配置变更事件。 |
| Protocol logic | No | 不启动、不修改 DoIP/UDS/DID/DTC/Flash/TLS 运行时行为。 |

## Architecture Considerations

- API 应复用 `task-003` 的 `SimulatorConfig` 与 `ConfigStore`，避免复制配置读写或校验逻辑。
- 字段级错误响应应由 `ConfigValidationException` 或 `ConfigValidationResult` 映射而来，保持错误来源单一。
- 配置变更事件应是保存成功后的应用内通知，不应隐式热重载复杂协议状态。
- `PUT /api/config` 必须以完整 `SimulatorConfig` 为请求体；缺字段或非法字段应走同一字段级校验错误路径。
- API 接线应保持最小化，维持 `task-002` 的轻量 WebApi 启动模型。

## Acceptance Criteria

- [ ] `GET /api/config` 返回当前配置。
- [ ] `PUT /api/config` 保存合法完整 `SimulatorConfig`。
- [ ] 非法配置返回 HTTP `400`。
- [ ] 非法配置响应包含字段级错误。
- [ ] 保存成功后发出配置变更事件。
- [ ] 保存后重启服务或重建配置存储能加载新配置。
- [ ] Scope check 确认未加入局部 `PATCH`、权限系统、UI、复杂协议热应用或 DoIP/UDS 运行时行为。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 配置 API 接线扩大为通用配置管理系统 | Medium | Medium | 仅实现两个指定端点和必要依赖注入，避免额外管理能力。 |
| 配置变更事件被误实现为复杂运行时热应用 | Medium | High | 事件只表示保存成功后的通知，不驱动 DoIP/UDS 协议状态更新。 |
| 校验错误响应与 Core 校验模型重复或不一致 | Medium | Medium | 统一从现有配置校验结果映射 HTTP `400` 响应。 |
| 保存后的重启加载路径不稳定 | Medium | Medium | 用测试覆盖保存后重建 store/app 并读取同一路径的新配置。 |
