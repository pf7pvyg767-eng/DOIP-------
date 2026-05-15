# Spec: Vue 控制台基础页面

**Change ID:** `task-005`
**Status:** Draft

---

## ADDED Requirements

### Requirement: 可打开的 Web 控制台

The WebConsole SHALL provide a Vue-based dashboard page that is visible from the startup URL.

#### Scenario: 打开启动 URL 看到控制台
- **GIVEN** WebApi 和 WebConsole 已按现有启动方式运行
- **WHEN** 用户在浏览器中打开启动 URL
- **THEN** 页面 SHALL render the Web console dashboard
- **AND** 页面 SHALL include service health information area
- **AND** 页面 SHALL include configuration summary information area
- **AND** 页面 SHALL NOT require authentication or configuration editing before viewing the dashboard

### Requirement: 服务健康状态展示

The WebConsole SHALL read `GET /api/health` and display service health information.

#### Scenario: 展示服务状态、启动时间和版本
- **GIVEN** `GET /api/health` 返回成功响应
- **WHEN** Dashboard loads
- **THEN** WebConsole SHALL request `GET /api/health`
- **AND** WebConsole SHALL display the service status
- **AND** WebConsole SHALL display the service startup time when provided by the response
- **AND** WebConsole SHALL display the service version when provided by the response

#### Scenario: Health 字段缺失时保持页面可用
- **GIVEN** `GET /api/health` 返回成功响应但缺少启动时间或版本字段
- **WHEN** Dashboard renders health information
- **THEN** WebConsole SHALL keep the dashboard visible
- **AND** WebConsole SHALL show a clear placeholder for unavailable optional health fields

### Requirement: 当前配置摘要展示

The WebConsole SHALL read `GET /api/config` and display a read-only simulator configuration summary.

#### Scenario: 展示配置摘要
- **GIVEN** `GET /api/config` 返回当前 `SimulatorConfig`
- **WHEN** Dashboard loads
- **THEN** WebConsole SHALL request `GET /api/config`
- **AND** WebConsole SHALL display VIN
- **AND** WebConsole SHALL display EID
- **AND** WebConsole SHALL display GID
- **AND** WebConsole SHALL display logical address
- **AND** WebConsole SHALL display DoIP UDP port
- **AND** WebConsole SHALL display DoIP TCP port
- **AND** WebConsole SHALL display DoIP TLS port
- **AND** displayed configuration fields SHALL be read-only

#### Scenario: 页面刷新后重新展示当前数据
- **GIVEN** WebApi can return current health and configuration data
- **WHEN** 用户刷新 Dashboard 页面
- **THEN** WebConsole SHALL reload health and configuration data from the backend
- **AND** WebConsole SHALL display the current service status
- **AND** WebConsole SHALL display the current configuration summary

### Requirement: 加载态

The WebConsole SHALL show a basic loading state while dashboard data is being fetched.

#### Scenario: 首次加载显示加载态
- **GIVEN** Dashboard has started fetching `GET /api/health` and `GET /api/config`
- **WHEN** either request has not completed
- **THEN** WebConsole SHALL display a loading state
- **AND** WebConsole SHALL avoid showing stale or partial data as a completed dashboard

### Requirement: 错误态

The WebConsole SHALL show an error state when the backend is unavailable or dashboard API requests fail.

#### Scenario: 后端不可用时显示错误状态
- **GIVEN** WebApi is unavailable or cannot be reached
- **WHEN** Dashboard attempts to load `GET /api/health` or `GET /api/config`
- **THEN** WebConsole SHALL display an error state
- **AND** the error state SHALL indicate that dashboard data could not be loaded
- **AND** the page SHALL remain rendered rather than becoming blank

#### Scenario: API 返回非成功响应时显示错误状态
- **GIVEN** `GET /api/health` or `GET /api/config` returns a non-success HTTP response
- **WHEN** Dashboard handles the response
- **THEN** WebConsole SHALL display an error state
- **AND** WebConsole SHALL NOT display the failed response as valid dashboard data

### Requirement: 前端测试覆盖

The WebConsole SHALL include focused tests for dashboard rendering and API states.

#### Scenario: 成功状态测试
- **GIVEN** mocked `GET /api/health` and `GET /api/config` responses contain valid data
- **WHEN** frontend tests render the dashboard
- **THEN** the tests SHALL assert service health information is displayed
- **AND** the tests SHALL assert configuration summary fields are displayed

#### Scenario: 失败状态测试
- **GIVEN** mocked dashboard API requests fail
- **WHEN** frontend tests render the dashboard
- **THEN** the tests SHALL assert the error state is displayed

### Requirement: 基础控制台范围限制

The WebConsole dashboard SHALL remain limited to read-only service health and configuration summary display.

#### Scenario: 不实现配置编辑
- **GIVEN** task-005 is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT include configuration editing fields
- **AND** it SHALL NOT include save, update, or patch behavior for simulator configuration

#### Scenario: 不实现日志流或协议报文列表
- **GIVEN** task-005 is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT include log streaming views
- **AND** it SHALL NOT include protocol message list views
- **AND** it SHALL NOT add connection, DoIP, or UDS realtime observation views

#### Scenario: 不修改后端运行时行为
- **GIVEN** task-005 is implemented
- **WHEN** the backend and protocol implementation are inspected
- **THEN** it SHALL NOT add new backend API contracts beyond consuming existing `GET /api/health` and `GET /api/config`
- **AND** it SHALL NOT add DoIP or UDS runtime behavior

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
