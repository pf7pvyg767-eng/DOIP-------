# Spec: 配置读取与保存 API

**Change ID:** `task-004`
**Status:** Draft

---

## ADDED Requirements

### Requirement: 配置读取 API

WebApi SHALL expose `GET /api/config` to return the current simulator configuration.

#### Scenario: 读取当前配置
- **GIVEN** WebApi 已启动并能访问配置存储
- **WHEN** 客户端发送 `GET /api/config`
- **THEN** WebApi SHALL return HTTP `200`
- **AND** 响应体 SHALL contain the current `SimulatorConfig`
- **AND** 当配置文件缺失时，响应体 SHALL contain the valid default configuration provided by the configuration subsystem

### Requirement: 完整配置保存 API

WebApi SHALL expose `PUT /api/config` to save a complete `SimulatorConfig`.

#### Scenario: 保存合法完整配置
- **GIVEN** 客户端提供合法且完整的 `SimulatorConfig`
- **WHEN** 客户端发送 `PUT /api/config`
- **THEN** WebApi SHALL validate the submitted configuration
- **AND** WebApi SHALL persist the submitted configuration
- **AND** WebApi SHALL return a successful HTTP response
- **AND** 后续 `GET /api/config` SHALL return the saved configuration

#### Scenario: 保存后重新加载配置
- **GIVEN** `PUT /api/config` 已成功保存合法配置
- **WHEN** 服务重启或配置存储被重建并从同一配置位置加载配置
- **THEN** the configuration subsystem SHALL load the saved configuration
- **AND** the loaded configuration SHALL preserve the saved `SimulatorConfig` values

### Requirement: 配置校验错误响应

WebApi SHALL reject invalid submitted simulator configuration with HTTP `400` and field-level validation errors.

#### Scenario: 非法配置返回字段级错误
- **GIVEN** 客户端提供包含非法字段的 `SimulatorConfig`
- **WHEN** 客户端发送 `PUT /api/config`
- **THEN** WebApi SHALL return HTTP `400`
- **AND** 响应体 SHALL include error code `CONFIG_VALIDATION_FAILED`
- **AND** 响应体 SHALL include a validation failure message
- **AND** 响应体 SHALL include field-level error details with `path` and `message`
- **AND** WebApi SHALL NOT persist the invalid configuration

#### Scenario: 非法 VIN、端口或逻辑地址映射为字段级错误
- **GIVEN** 客户端提交的配置包含非法 VIN、非法端口或非法逻辑地址
- **WHEN** WebApi validates the submitted configuration
- **THEN** WebApi SHALL include an error detail for each invalid field reported by the configuration validator
- **AND** 每个错误 SHALL identify the affected field path
- **AND** 每个错误 SHALL include a clear human-readable message

### Requirement: 配置变更事件

WebApi SHALL emit a configuration change event after a valid configuration is successfully saved.

#### Scenario: 保存成功后发布配置变更事件
- **GIVEN** 客户端提供合法完整的 `SimulatorConfig`
- **WHEN** `PUT /api/config` successfully persists the configuration
- **THEN** the application SHALL emit a configuration change event
- **AND** the event SHALL identify that the simulator configuration changed
- **AND** the event SHALL be observable by application components or tests

#### Scenario: 非法配置不发布变更事件
- **GIVEN** 客户端提供非法 `SimulatorConfig`
- **WHEN** `PUT /api/config` is rejected with HTTP `400`
- **THEN** the application SHALL NOT emit a configuration change event

### Requirement: 配置 API 范围限制

The configuration API SHALL remain limited to reading and saving complete simulator configuration.

#### Scenario: 不支持局部 PATCH
- **GIVEN** 客户端尝试使用局部更新语义修改配置
- **WHEN** the task-004 implementation is inspected
- **THEN** it SHALL NOT include a `PATCH /api/config` endpoint
- **AND** it SHALL NOT provide partial configuration update behavior

#### Scenario: 不引入运行时协议热应用
- **GIVEN** 配置通过 `PUT /api/config` 保存成功
- **WHEN** the configuration change event is emitted
- **THEN** the implementation SHALL NOT apply complex DoIP or UDS protocol changes to a running runtime
- **AND** it SHALL NOT add DoIP, UDS, DID, DTC, Routine, Session, SecurityAccess, Flash, or TLS runtime behavior

#### Scenario: 不引入权限系统或 UI
- **GIVEN** task-004 is implemented
- **WHEN** the implementation is inspected
- **THEN** it SHALL NOT add authentication or authorization behavior
- **AND** it SHALL NOT add WebConsole UI or configuration editing screens

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
