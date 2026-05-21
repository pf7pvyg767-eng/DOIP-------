# Implementation Tasks: 配置读取与保存 API

**Change ID:** `task-004`

---

## Phase 1: API Contract And Wiring

- [x] 1.1 为 WebApi 接入配置读取/保存所需的 `ConfigStore` 与配置文件路径。
- [x] 1.2 增加 `GET /api/config`，返回当前 `SimulatorConfig`。
- [x] 1.3 增加 `PUT /api/config`，接收完整 `SimulatorConfig` 请求体。
- [x] 1.4 保持 API 范围仅限配置读取与完整配置保存，未增加 `PATCH`、权限系统或 UI。

**Quality Gate:**
- [x] WebApi 编译通过。
- [x] API 只暴露 `GET /api/config` 与 `PUT /api/config` 两个配置端点。

---

## Phase 2: Validation And Error Response

- [x] 2.1 复用 `task-003` 的配置校验逻辑校验 `PUT /api/config` 请求体。
- [x] 2.2 将非法配置映射为 HTTP `400`。
- [x] 2.3 在错误响应中返回 `CONFIG_VALIDATION_FAILED`、统一错误消息和字段级 `path`/`message` 详情。
- [x] 2.4 为缺失或无法反序列化的请求体提供清晰错误响应，且不保存无效配置。

**Quality Gate:**
- [x] 非法 VIN、端口或逻辑地址可返回字段级错误。
- [x] 无效配置不会覆盖现有有效配置。

---

## Phase 3: Persistence And Change Event

- [x] 3.1 使用 `ConfigStore` 保存合法完整 `SimulatorConfig`。
- [x] 3.2 保存成功后发出配置变更事件。
- [x] 3.3 配置变更事件不执行复杂协议热应用或 DoIP/UDS 运行时行为。
- [x] 3.4 保存后重启服务或重建 `ConfigStore` 可读取新配置。

**Quality Gate:**
- [x] 合法配置保存后可重新加载。
- [x] 保存成功事件可在测试中观察。
- [x] 事件保持通知语义，不触发协议运行时行为。

---

## Phase 4: Tests And Scope Check

- [x] 4.1 增加 API 测试：`GET /api/config` 返回默认或当前配置。
- [x] 4.2 增加 API 测试：`PUT /api/config` 保存合法完整配置。
- [x] 4.3 增加 API 测试：非法配置返回 HTTP `400` 和字段级错误。
- [x] 4.4 增加集成测试：保存后重建服务或 `ConfigStore` 能加载新配置。
- [x] 4.5 增加测试断言：保存成功后发出配置变更事件。
- [x] 4.6 执行 scope check，确认未加入 `PATCH`、权限系统、UI、复杂协议热应用或 DoIP/UDS 运行时行为。

**Quality Gate:**
- [x] `openspec validate task-004 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] `GET /api/config` 已实现。
- [x] `PUT /api/config` 已实现。
- [x] `GET /api/config` 返回当前 `SimulatorConfig`。
- [x] `PUT /api/config` 保存合法完整 `SimulatorConfig`。
- [x] 非法配置返回 HTTP `400`。
- [x] 非法配置响应包含字段级错误。
- [x] 保存成功后发出配置变更事件。
- [x] 保存后重启服务或重建配置存储能加载新配置。
- [x] 未实现局部 `PATCH`。
- [x] 未实现权限系统。
- [x] 未实现 UI。
- [x] 未加入复杂协议热应用或 DoIP/UDS 运行时行为。
- [x] OpenSpec 严格校验已执行。
- [x] 后端 build 已执行。
- [x] 后端 test 已执行。
