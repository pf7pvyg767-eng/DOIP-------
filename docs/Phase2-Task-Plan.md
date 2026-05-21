# 阶段 2 任务计划：核心功能产品化与动态虚拟 ECU

## 1. 阶段 2 目标重新定位

阶段 2 的主要目标不是继续扩大功能清单，也不是优先补一套繁重的验收流程，而是把第一阶段已经铺好的能力做真、做顺、做成用户可以直接使用的产品功能。

本阶段最重要的三条主线是：

1. Web Console 进入后立即告诉用户系统当前是否可用，以及诊断仪应该如何连接本模拟器。
2. Web Console 提供真实的运行时控制能力，包括受控 shutdown，并能释放 DoIP/Web API 端口。
3. 虚拟 ECU 的 DID 不再只返回 JSON 中固定值，而是支持动态值，例如正弦函数、随机数、线性函数或内置表达式，并在 Web Console 中实时绘图。

Phase2 P0 的完成标准是：用户打开 UI 后能看懂当前系统、能连接诊断仪、能看到真实诊断交互、能配置动态 DID、能看到 DID 实时曲线、能从 UI 停止系统。

## 2. 当前代码状态与目标差距

### 2.1 已具备基础

- Host 已经能启动 Web API、UDP DoIP、TCP DoIP、可选 TLS、事件流、连接注册表、配置、DID/DTC runtime store、UDS dispatcher 和 PCAP recorder。
- Web API 已有 `/api/health`、`/api/config`、`/api/connections`、`/api/metrics`、`/api/ecu/state`、`/api/dids`、`/api/pcap/*` 和事件流接口。
- Web Console 已有 Overview、Diagnostics、Data、Capture、Events 等区域，并已接入真实 API 和 WebSocket 事件。
- `0x22` ReadDataByIdentifier 已从 `DidRuntimeStore` 读取当前 DID runtime value，而不是完全写死在服务内部。
- DID 目前可以通过 Web Console 手动写入固定 hex 值。

### 2.2 关键差距

- 缺少 UI shutdown 闭环。Host 目前依赖 Ctrl+C/进程取消路径，Web API 和 Web Console 未提供受控 shutdown 操作。
- UI 首页没有足够明确的“系统现在怎么连接”信息。用户进入 Web Console 后，需要直接看到 IP、DoIP UDP/TCP/TLS 端口、ECU logical address、tester source address 白名单、VIN、配置路径、当前连接状态和下一步操作。
- 当前 DID 模型仍以静态 JSON 值为核心。`DidRuntimeStore.TryRead()` 返回的是内存中的固定 byte array，`0x22` 每次读到的值不随时间自动变化。
- Web Console 的 DID 面板只支持查看和写入 hex 值，没有动态值 provider 的配置入口。
- Web Console 没有 DID 实时曲线。即使诊断仪持续读取 DID，用户也无法直观看到 DID 数值随时间变化。
- Runtime event 中有 `uds.did.read` 事件，但事件数据不足以支持 DID 曲线绘制，例如缺少当前数值、数值类型、采样时间、数值单位等信息。

## 3. P0 功能范围

P0 只做能让产品明显可用的核心能力：

- UI 运行状态与连接指引。
- UI shutdown。
- 静态 DID 继续兼容。
- 动态 DID provider。
- `0x22` 读取动态 DID 实时值。
- Web Console 配置动态 DID。
- Web Console 实时显示 DID 曲线。
- 必要的单元测试、传输层测试和轻量手工/脚本验证。

P0 暂不做：

- 任意用户脚本执行。
- 复杂脚本沙箱。
- 完整测试报告平台。
- 完整浏览器 E2E 测试矩阵。
- MSI 安装验证。
- 复杂 OEM 场景 runner。

## 4. 动态 DID 设计原则

### 4.1 Provider 类型

第一版支持以下 DID provider：

- `static`：保持当前行为，返回配置或运行时写入的固定 hex 值。
- `random`：按配置范围生成随机数。
- `sine`：按振幅、偏移、周期生成正弦值。
- `linear`：按起始值、斜率和时间生成线性变化值。

`function` 作为后续扩展方向，但 P0 不执行任意用户脚本。原因是脚本执行会引入安全、超时、异常隔离和可复现性问题，容易拖慢核心功能落地。

### 4.2 数据类型

动态 DID 第一版支持数值型 DID：

- `uint8`
- `uint16`
- `uint32`
- `int16`
- `int32`

编码默认使用 big-endian，匹配 UDS 常见网络字节序。

字符串型 DID，例如 VIN，默认仍使用 `static`。

### 4.3 运行时行为

- 每次诊断仪发送 `0x22` 时，系统根据当前时间计算 DID 值，再编码成 byte array 返回。
- Web Console 可以按固定刷新频率拉取或订阅 DID sample。
- 动态 DID 可以被 `0x2E` 写入时的行为需要明确：
  - `static` DID 保持现有可写逻辑。
  - 动态 DID 默认不可写。
  - 如果后续需要动态 DID 可写，应设计为修改 provider 参数，而不是直接覆盖当前值。

## 5. 任务拆解

### Task 1：UI 运行状态与连接指引

目标：用户打开 Web Console 后，第一眼就知道模拟器是否运行、诊断仪该如何连接。

影响文件：

- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`
- `src/DoipSimulator.WebConsole/src/styles.css`

实施内容：

- Web API 提供 runtime summary，包含：
  - Web API address/port。
  - DoIP UDP port。
  - DoIP TCP port。
  - DoIP TLS port 和启用状态。
  - VIN。
  - ECU logical address。
  - tester source address whitelist。
  - config path。
  - startedAt。
  - process id。
  - active connection count。
- Web Console Overview 顶部显示“连接本模拟器”的信息区。
- UI 用状态分层表达：
  - `API Ready`
  - `Waiting for DoIP Discovery`
  - `TCP Connected`
  - `Routing Activated`
  - `UDS Traffic Active`
- Diagnostics 面板保留原有连接与报文列表，但 Overview 要给用户最短路径。

验收：

- 启动 Host 后打开 Web Console，不需要阅读文档即可看到诊断仪连接参数。
- 当诊断仪连接并 routing activation 后，状态从等待连接更新为已激活。
- 所有显示数据来自真实 API 或 runtime event。

### Task 2：受控 Runtime Shutdown

目标：用户可以从 Web Console 停止模拟器，并释放端口。

影响文件：

- `src/DoipSimulator.Host/Program.cs`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`
- `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- `tests/DoipSimulator.Core.Tests` 或新增 Web API 测试

实施内容：

- Host 创建 runtime shutdown signal，并传给 Web API。
- Web API 增加 `POST /api/runtime/shutdown`。
- shutdown endpoint 发布 `system.shutdown.requested` 事件。
- 如果 PCAP 正在录制，shutdown 前停止当前 PCAP session。
- Web Console 增加 shutdown 按钮和确认弹窗。
- 触发 shutdown 后 UI 显示“正在停止”，随后显示 API disconnected 状态。

验收：

- 调用 `POST /api/runtime/shutdown` 后 Host 进程退出。
- DoIP TCP/UDP 端口释放。
- Web API 端口释放。
- Web Console shutdown 操作需要确认，避免误点。

### Task 3：动态 DID 配置模型

目标：在配置层支持 DID value provider，同时兼容现有静态 DID。

影响文件：

- `src/DoipSimulator.Core/Configuration/SimulatorConfig.cs`
- `src/DoipSimulator.Core/Configuration/ConfigValidation.cs`
- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `sample-config/default.simulator.json`
- `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs`

实施内容：

- 为 `DidConfig` 增加动态配置字段，例如：
  - `ValueProvider.Type`
  - `ValueProvider.NumericType`
  - `ValueProvider.Min`
  - `ValueProvider.Max`
  - `ValueProvider.Amplitude`
  - `ValueProvider.Offset`
  - `ValueProvider.PeriodMs`
  - `ValueProvider.SlopePerSecond`
  - `ValueProvider.Seed`
- 未配置 `ValueProvider` 时等价于 `static`。
- 配置验证保证：
  - `static` DID 必须有合法 hex value。
  - `random` 必须有 numeric type、min、max。
  - `sine` 必须有 numeric type、amplitude、offset、periodMs。
  - `linear` 必须有 numeric type、offset、slopePerSecond。
  - 动态 DID 的 encoded length 与 numeric type 一致。

验收：

- 现有静态 DID 测试继续通过。
- 新增 random/sine/linear 配置验证测试。
- `sample-config/default.simulator.json` 增加至少 2 个动态 DID 示例。

### Task 4：动态 DID 运行时计算

目标：`0x22` 读取 DID 时返回实时计算值。

影响文件：

- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/ReadDataByIdentifierServiceTests.cs`
- `tests/DoipSimulator.Transport.Tests/TcpDoipServerTests.cs`

实施内容：

- 在 Core 中引入 DID value provider 计算逻辑。
- `DidRuntimeStore.TryRead()` 对 static DID 返回当前固定值。
- `DidRuntimeStore.TryRead()` 对动态 DID 根据当前时间计算数值并编码。
- 为测试引入可控 time provider，避免正弦/线性测试不稳定。
- `ReadDataByIdentifierService` 不需要知道 provider 细节，只从 store 读取 byte array。

验收：

- 同一个 sine DID 在不同时间读取返回不同值。
- random DID 在范围内返回合法编码值。
- linear DID 按时间变化。
- `0x22` 通过 TCP DoIP 读取动态 DID 时返回实时值。

### Task 5：DID 采样事件与 API

目标：让 Web Console 可以获取 DID 实时值，用于绘图。

影响文件：

- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.Core/RuntimeEvents`
- `tests/DoipSimulator.Core.Tests`
- `tests/DoipSimulator.Protocols.Uds.Tests`

实施内容：

- `uds.did.read` 事件增加：
  - did。
  - raw hex value。
  - numeric value，如果 DID 是数值型。
  - provider type。
  - sampledAt。
  - connectionId。
- Web API 增加 `GET /api/dids/{did}/sample`，返回当前 DID sample。
- Web API 增加 `GET /api/dids/samples`，返回所有可采样 DID 当前值。
- 对静态非数值 DID，返回 raw hex，不返回 numeric value。

验收：

- 诊断仪读取 DID 后，事件流中出现带数值的 DID sample。
- Web API 可直接读取当前 DID sample。
- 动态 DID 不依赖诊断仪请求也可以被 Web Console 采样显示。

### Task 6：Web Console 动态 DID 配置

目标：用户能在 Web 端查看 DID 是静态还是动态，并配置动态参数。

影响文件：

- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/components/DidEditorPanel.vue`
- `src/DoipSimulator.WebConsole/src/styles.css`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`

实施内容：

- DID 列表显示 provider 类型。
- 静态 DID 继续显示 hex value 写入表单。
- 动态 DID 显示参数表单：
  - random：numeric type、min、max。
  - sine：numeric type、amplitude、offset、period。
  - linear：numeric type、offset、slope。
- Web API 增加 DID provider 更新接口，例如 `PUT /api/dids/{did}/provider`。
- 更新 provider 后立即影响后续 `0x22` 响应。

验收：

- 在 UI 中把一个 DID 从 static 改为 sine 后，诊断仪读取值开始随时间变化。
- 修改参数后无需重启 Host。
- 非法参数在 UI 中显示明确错误。

### Task 7：Web Console DID 实时曲线

目标：Web Console 实时显示动态 DID 的当前值和历史变化。

影响文件：

- `src/DoipSimulator.WebConsole/src/components/DidEditorPanel.vue`
- 新建 `src/DoipSimulator.WebConsole/src/components/DidLiveChartPanel.vue`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/styles.css`
- `src/DoipSimulator.WebConsole/package.json`

实施内容：

- 增加 DID 曲线面板。
- 支持选择一个或多个数值 DID。
- 每个 DID 保留最近 60 秒或最近 300 个采样点。
- 优先使用 WebSocket 中的 DID read/sample event 更新曲线。
- 如果没有诊断仪读取，也可以按固定周期调用 sample API，让曲线继续展示模拟值。
- 图表库优先选择轻量依赖；如果不引入依赖，则用 SVG 或 Canvas 绘制简单折线。

验收：

- sine DID 曲线呈周期变化。
- random DID 曲线在配置范围内跳动。
- linear DID 曲线按斜率变化。
- 切换 DID 时图表不崩溃、不显示旧 DID 的错误数据。

### Task 8：连接指引与动态 DID 的轻量验收

目标：只为核心功能增加必要验证，不让测试流程喧宾夺主。

影响文件：

- `runs/local-dev/doip-uds-smoke-temp.ps1`
- 新建或更新 `scripts/phase2-functional-smoke.ps1`
- `README.md`
- `docs/Phase2-Task-Plan.md`

实施内容：

- smoke 脚本覆盖：
  - API health。
  - runtime summary。
  - UDP discovery。
  - routing activation。
  - `0x22` 读取 static DID。
  - `0x22` 读取 dynamic DID。
  - sample API 返回数值。
  - shutdown API 可用。
- 不把 MSI 安装、完整 UI E2E、完整报告系统放入每次任务测试。

验收：

- `.\scripts\phase2-functional-smoke.ps1` 能验证核心功能。
- 输出清楚指出每个功能是否通过。

## 6. 推荐执行顺序

1. Task 1：UI 运行状态与连接指引。
2. Task 2：受控 Runtime Shutdown。
3. Task 3：动态 DID 配置模型。
4. Task 4：动态 DID 运行时计算。
5. Task 5：DID 采样事件与 API。
6. Task 6：Web Console 动态 DID 配置。
7. Task 7：Web Console DID 实时曲线。
8. Task 8：连接指引与动态 DID 的轻量验收。

这个顺序的原因是：先让用户知道系统怎么连，再让用户能控制系统生命周期，然后把虚拟 ECU 的核心行为从固定值升级为动态值，最后补 UI 曲线和轻量验收。

## 7. 建议拆分为 OpenSpec Changes

- `phase2-runtime-status-and-connection-guide`
  - 覆盖 Task 1。
- `phase2-ui-runtime-shutdown`
  - 覆盖 Task 2。
- `phase2-dynamic-did-provider`
  - 覆盖 Task 3、Task 4、Task 5。
- `phase2-dynamic-did-console`
  - 覆盖 Task 6、Task 7。
- `phase2-functional-smoke`
  - 覆盖 Task 8。

## 8. P0 完成判定

P0 完成时必须满足：

- 用户打开 Web Console 后能直接看到诊断仪连接参数。
- 诊断仪可以按 UI 指引完成 DoIP discovery、routing activation 和 `0x22` 读取。
- Web Console 能显示当前连接状态和最近诊断流量。
- Web Console 可以触发 shutdown，并释放端口。
- 至少一个 DID 使用 `static` provider。
- 至少一个 DID 使用 `sine` provider。
- 至少一个 DID 使用 `random` 或 `linear` provider。
- 诊断仪读取动态 DID 时，返回值随时间变化。
- Web Console 能实时显示动态 DID 曲线。
- 动态 DID 修改参数后无需重启 Host。
- 轻量 functional smoke 通过。

## 9. 阶段后续方向

P0 稳定后再考虑：

- 更复杂的 DID 表达式 provider。
- 安全沙箱中的用户自定义函数。
- 多场景 profile 切换。
- 完整浏览器自动化测试。
- MSI 安装验证。
- TLS key log 与 PCAP 深度分析。
