# web-console-dashboard Specification

## Purpose
TBD - created by archiving change task-005. Update Purpose after archive.
## Requirements
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
The WebConsole dashboard SHALL remain limited to service health, configuration summary, runtime connection guidance, high-level runtime status display, and controlled runtime shutdown.

#### Scenario: 不实现配置编辑
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL NOT include configuration editing fields
- **AND** it SHALL NOT include save, update, or patch behavior for simulator configuration

#### Scenario: 允许连接指引和运行时停止但不实现诊断控制
- **GIVEN** this change is implemented
- **WHEN** the WebConsole implementation is inspected
- **THEN** it MAY consume read-only runtime summary, connection snapshot, and runtime event data
- **AND** it MAY send a controlled runtime shutdown request through `POST /api/runtime/shutdown`
- **AND** it SHALL NOT add controls that send DoIP or UDS diagnostic messages
- **AND** it SHALL NOT add configuration mutation behavior

#### Scenario: 不修改后端协议行为
- **GIVEN** this change is implemented
- **WHEN** the backend and protocol implementation are inspected
- **THEN** it SHALL NOT add new DoIP or UDS protocol behavior
- **AND** it SHALL NOT change existing Routing Activation, Vehicle Identification, UDS dispatcher, DID, DTC, Routine, Flash, TLS, PCAP packet content, or fault injection semantics except for orderly runtime shutdown cleanup

### Requirement: Connection Guide Overview
The WebConsole dashboard SHALL display a first-screen connection guide using real runtime summary data.

#### Scenario: Display diagnostic tester connection parameters
- **GIVEN** `GET /api/runtime/summary` returns runtime connection data
- **WHEN** the WebConsole dashboard loads
- **THEN** the Overview SHALL display the Web API endpoint
- **AND** the Overview SHALL display the DoIP UDP port
- **AND** the Overview SHALL display the DoIP TCP port
- **AND** the Overview SHALL display the DoIP TLS port and TLS enabled state
- **AND** the Overview SHALL display the ECU VIN
- **AND** the Overview SHALL display the ECU logical address
- **AND** the Overview SHALL display the tester source address whitelist
- **AND** the Overview SHALL display the configuration path when available
- **AND** the Overview SHALL display the runtime start timestamp
- **AND** the Overview SHALL display the process ID
- **AND** the Overview SHALL display the active connection count

#### Scenario: Runtime summary load failure keeps dashboard usable
- **GIVEN** `GET /api/runtime/summary` fails
- **WHEN** the WebConsole dashboard handles the failure
- **THEN** the dashboard SHALL remain rendered
- **AND** the connection guide SHALL show a clear unavailable state
- **AND** existing health and configuration dashboard sections SHALL remain usable when their APIs succeed

### Requirement: Connection Guide Uses Real Data
The WebConsole connection guide SHALL use backend runtime data rather than hard-coded or mock connection values.

#### Scenario: Backend port values are reflected
- **GIVEN** the simulator is started with non-default Web API or DoIP port values
- **WHEN** the WebConsole dashboard renders the connection guide
- **THEN** the displayed port values SHALL match the values returned by `GET /api/runtime/summary`
- **AND** the displayed values SHALL NOT be hard-coded defaults

#### Scenario: Source whitelist is displayed from configuration
- **GIVEN** the simulator configuration contains one or more tester source addresses in the whitelist
- **WHEN** the WebConsole dashboard renders the connection guide
- **THEN** the displayed tester source addresses SHALL match the runtime summary response

### Requirement: Dashboard Runtime Shutdown Control
The WebConsole dashboard SHALL provide a controlled runtime shutdown action.

#### Scenario: Show shutdown action in overview
- **GIVEN** the WebConsole dashboard has loaded successfully
- **WHEN** the user views the runtime overview or service status area
- **THEN** the dashboard SHALL display a shutdown action for stopping the current simulator runtime
- **AND** the action SHALL be visually distinct from read-only status fields

#### Scenario: Require confirmation before shutdown
- **GIVEN** the shutdown action is visible
- **WHEN** the user activates the shutdown action
- **THEN** the WebConsole SHALL show a confirmation prompt or dialog before calling the backend shutdown API
- **AND** cancelling the confirmation SHALL NOT call `POST /api/runtime/shutdown`

#### Scenario: Confirm shutdown request
- **GIVEN** the confirmation prompt is visible
- **WHEN** the user confirms shutdown
- **THEN** the WebConsole SHALL call `POST /api/runtime/shutdown`
- **AND** the WebConsole SHALL enter a stopping state
- **AND** the WebConsole SHALL disable repeated shutdown submissions while stopping

#### Scenario: Display disconnected state after shutdown
- **GIVEN** a shutdown request has been confirmed
- **WHEN** subsequent WebApi requests fail because the runtime has stopped
- **THEN** the WebConsole SHALL display a clear stopped or disconnected state
- **AND** the page SHALL remain rendered rather than becoming blank
- **AND** the disconnected state SHALL NOT be presented as an unexpected dashboard load failure

#### Scenario: Show shutdown failure
- **GIVEN** the user confirms shutdown
- **WHEN** `POST /api/runtime/shutdown` fails before shutdown is accepted
- **THEN** the WebConsole SHALL display a clear failure state
- **AND** the shutdown action SHALL become available again unless the runtime is already disconnected

### Requirement: Overview Hosts Runtime Cockpit
The WebConsole Overview SHALL host the runtime cockpit as the primary first-screen connection experience.

#### Scenario: Render runtime cockpit in Overview
- **GIVEN** the WebConsole dashboard has loaded successfully
- **WHEN** the user views the Overview workspace
- **THEN** the Overview SHALL display the runtime cockpit workflow
- **AND** the Overview SHALL keep the existing workspace navigation visible
- **AND** the Overview SHALL keep the top telemetry strip visible
- **AND** the Overview SHALL keep the realtime observation rail visible when the rail is available

#### Scenario: Preserve controlled shutdown from cockpit
- **GIVEN** the runtime cockpit is visible
- **WHEN** the user activates the runtime shutdown action
- **THEN** the WebConsole SHALL use the existing controlled shutdown confirmation flow
- **AND** it SHALL call `POST /api/runtime/shutdown` only after confirmation

#### Scenario: Preserve real dashboard data sources
- **GIVEN** the runtime cockpit renders connection and evidence information
- **WHEN** the WebConsole implementation is inspected
- **THEN** it SHALL use backend API responses and runtime events as the data source
- **AND** it SHALL NOT introduce production mock data for cockpit state

### Requirement: Overview Cockpit Visual Fit
The Overview runtime cockpit SHALL match the existing WebConsole visual system.

#### Scenario: Match existing control desk layout
- **GIVEN** the runtime cockpit is rendered
- **WHEN** the user views the Overview at desktop width
- **THEN** the cockpit SHALL use the existing dark control-desk palette
- **AND** it SHALL use compact panel spacing consistent with existing WebConsole sections
- **AND** it SHALL avoid replacing the existing app shell with a separate landing-page style layout

#### Scenario: Avoid unusable overflow
- **GIVEN** the runtime cockpit is rendered at common desktop widths
- **WHEN** the user views the Overview
- **THEN** cockpit text and controls SHALL remain readable
- **AND** step list, detail panel, and evidence summary SHALL NOT overlap each other

### Requirement: Runtime Cockpit Lightweight Smoke
The project SHALL include a lightweight smoke check for runtime cockpit UI integration.

#### Scenario: Smoke verifies cockpit integration
- **GIVEN** the runtime cockpit UI has been implemented
- **WHEN** the runtime cockpit smoke command runs
- **THEN** it SHALL verify the expected cockpit source files exist
- **AND** it SHALL verify the Overview references the runtime cockpit
- **AND** it SHALL run the frontend production build

#### Scenario: Smoke excludes heavyweight release checks
- **GIVEN** the runtime cockpit smoke command runs during daily development
- **WHEN** the smoke completes
- **THEN** it SHALL NOT require MSI installation validation
- **AND** it SHALL NOT require a full browser E2E suite

