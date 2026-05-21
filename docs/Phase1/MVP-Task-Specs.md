# MVP Task Specs：Windows DoIP ECU 模拟器

本文把 `MVP-Development-Roadmap.md` 拆成适合 AI coding agent 逐步完成的任务规格。每个任务目标控制在约 0.5 到 2 天人类工程量，要求能单独开发、测试、提交。

## 默认工程假设

为让任务具备可执行的文件边界，本文采用以下默认工程结构：

```text
src/
  DoipSimulator.Host/              # 命令行启动入口、运行时编排
  DoipSimulator.Core/              # 配置、事件、状态模型、共享契约
  DoipSimulator.Transport/         # UDP/TCP/TLS 网络传输
  DoipSimulator.Protocols.Doip/    # ISO 13400 编解码和 DoIP 流程
  DoipSimulator.Protocols.Uds/     # ISO 14229 服务分发和 UDS 服务
  DoipSimulator.Security/          # TLS 证书、安全算法插件
  DoipSimulator.Observability/     # 日志、事件流、pcap、指标
  DoipSimulator.WebApi/            # Web 控制面 API
  DoipSimulator.WebConsole/        # Vue/Vite 控制台
tests/
  DoipSimulator.*.Tests/
sample-config/
docs/
```

若后续技术栈变化，保持任务目标、接口契约和验收标准不变，替换文件路径即可。

## 通用提交约束

- 每个 Task 单独提交，不混入无关重构。
- 每个 Task 完成后服务应能启动，已有测试应通过。
- 新模块先定义窄接口，再实现最小可用行为。
- 只在当前 Task 范围内调整公共 contract。
- UI 只通过 WebApi 操作内核，不直接访问协议对象。
- ODX、Web、UDS 都使用统一配置模型，不各自发明数据结构。

## Task 01：建立解决方案骨架和开发入口

### Goal

建立可编译、可运行、可测试的工程骨架，为后续模块提供稳定目录和构建入口。

### Scope

- 创建后端解决方案和基础项目。
- 创建 Vue/Vite 前端项目壳。
- 增加统一构建、测试、运行脚本。
- 增加 `.gitignore`、基础 README、目录说明。
- 后端 Host 能输出占位启动信息。

### Out of Scope

- 不实现 DoIP、UDS、配置加载。
- 不实现真实 Web 控制台页面。
- 不接入数据库或外部服务。

### Files likely touched

- `DoipSimulator.sln`
- `src/DoipSimulator.Host/`
- `src/DoipSimulator.Core/`
- `src/DoipSimulator.WebApi/`
- `src/DoipSimulator.WebConsole/`
- `tests/DoipSimulator.Core.Tests/`
- `.gitignore`
- `README.md`

### API / data contract

暂无业务 API。只要求命令行入口：

```text
doip-simulator --help
doip-simulator run
```

### Acceptance Criteria

- 后端解决方案可编译。
- 前端项目可安装依赖并启动开发服务器。
- `doip-simulator run` 能启动并打印占位信息。
- 单元测试框架可运行，即使只有一个占位测试。

### Test Plan

- 运行后端 build。
- 运行后端 test。
- 运行前端 build。
- 执行 `doip-simulator --help` 和 `doip-simulator run`。

## Task 02：运行时启动、端口检查和健康检查

### Goal

实现命令行启动服务后输出本地 URL，并提供最小健康检查接口。

### Scope

- Host 启动 WebApi。
- 支持配置 Web 控制台监听地址和端口。
- 启动时检查端口占用。
- 控制台输出 `http://127.0.0.1:{port}`。
- 支持 Ctrl+C 优雅退出。
- 提供 `GET /api/health`。

### Out of Scope

- 不加载完整 ECU 配置。
- 不启动 DoIP 网络服务。
- 不实现前端业务页面。

### Files likely touched

- `src/DoipSimulator.Host/Program.cs`
- `src/DoipSimulator.Host/RuntimeOptions.cs`
- `src/DoipSimulator.WebApi/HealthController.cs`
- `src/DoipSimulator.WebApi/WebApiModule.cs`
- `tests/DoipSimulator.Host.Tests/`
- `tests/DoipSimulator.WebApi.Tests/`

### API / data contract

```http
GET /api/health
```

```json
{
  "status": "ok",
  "version": "0.1.0",
  "startedAt": "2026-05-15T14:00:00Z"
}
```

### Acceptance Criteria

- 命令行启动后输出可访问 URL。
- `GET /api/health` 返回 `200`。
- 端口占用时启动失败并提示端口号。
- Ctrl+C 后进程退出，端口释放。

### Test Plan

- 单元测试：健康检查返回结构正确。
- 集成测试：随机端口启动 WebApi 并请求 `/api/health`。
- 手工测试：启动两个相同端口实例，第二个应明确失败。

## Task 03：配置模型、默认配置和校验

### Goal

建立统一配置模型，作为 DoIP、UDS、Web、ODX 的共同数据契约。

### Scope

- 定义 `SimulatorConfig`。
- 支持 JSON 配置加载和保存。
- 缺失配置时生成默认配置。
- 实现基础校验：VIN、EID、GID、逻辑地址、端口、白名单。
- 预留 DID、DTC、Routine、Session、SecurityAccess、Flash、TLS 配置结构。

### Out of Scope

- 不支持 YAML。
- 不支持 ODX/PDX 导入。
- 不实现 Web 编辑。
- 不实现配置版本迁移。

### Files likely touched

- `src/DoipSimulator.Core/Configuration/SimulatorConfig.cs`
- `src/DoipSimulator.Core/Configuration/ConfigValidator.cs`
- `src/DoipSimulator.Core/Configuration/ConfigStore.cs`
- `src/DoipSimulator.Host/appsettings.json`
- `sample-config/default.simulator.json`
- `tests/DoipSimulator.Core.Tests/Configuration/`

### API / data contract

```json
{
  "entity": {
    "vin": "LTEST000000000001",
    "eid": "001122334455",
    "gid": "AABBCCDDEEFF",
    "logicalAddress": "0x0E00"
  },
  "network": {
    "bindAddress": "0.0.0.0",
    "doipUdpPort": 13400,
    "doipTcpPort": 13400,
    "doipTlsPort": 3496,
    "sourceAddressWhitelist": ["0x0E80"]
  },
  "uds": {
    "dids": [],
    "dtcs": [],
    "routines": [],
    "sessions": [],
    "securityAccess": [],
    "flash": null
  },
  "tls": {
    "enabled": false,
    "serverCertificatePath": null,
    "serverPrivateKeyPath": null,
    "clientCaPath": null,
    "requireClientCertificate": false
  }
}
```

### Acceptance Criteria

- 默认配置可生成并通过校验。
- 合法配置可加载为强类型对象。
- 非法 VIN、非法端口、非法逻辑地址返回明确错误。
- 保存后重新加载数据一致。

### Test Plan

- 单元测试：默认配置校验通过。
- 单元测试：非法字段分别触发对应错误码。
- 单元测试：JSON round-trip 不丢字段。

## Task 04：配置读取与保存 API

### Goal

让 Web 控制台可以读取和保存配置，为后续 UI 配置闭环铺路。

### Scope

- 增加配置 API。
- 返回当前配置和校验错误。
- 支持保存完整配置。
- 保存成功后发出配置变更事件。

### Out of Scope

- 不支持局部 PATCH。
- 不支持运行中热应用复杂协议变更。
- 不做权限系统。

### Files likely touched

- `src/DoipSimulator.WebApi/Controllers/ConfigController.cs`
- `src/DoipSimulator.Core/Configuration/ConfigStore.cs`
- `src/DoipSimulator.Core/Events/`
- `tests/DoipSimulator.WebApi.Tests/ConfigControllerTests.cs`

### API / data contract

```http
GET /api/config
PUT /api/config
```

`PUT /api/config` 请求体为 `SimulatorConfig`。

错误响应：

```json
{
  "code": "CONFIG_VALIDATION_FAILED",
  "message": "Config validation failed.",
  "details": [
    { "path": "entity.vin", "message": "VIN must be 17 characters." }
  ]
}
```

### Acceptance Criteria

- `GET /api/config` 返回当前配置。
- `PUT /api/config` 保存合法配置。
- 非法配置返回 `400` 和字段级错误。
- 保存后重启服务能加载新配置。

### Test Plan

- API 测试：读取默认配置。
- API 测试：保存合法配置。
- API 测试：保存非法配置返回 400。
- 集成测试：保存后重新构建 `ConfigStore` 可读取新值。

## Task 05：Vue 控制台基础页面

### Goal

形成可打开的 Web 控制台，展示服务健康状态和当前配置摘要。

### Scope

- 实现 Vue 页面布局。
- 展示服务状态、启动时间、版本。
- 展示 VIN、EID、GID、逻辑地址、端口。
- 提供基础错误态和加载态。

### Out of Scope

- 不实现配置编辑。
- 不实现日志流。
- 不实现协议报文列表。

### Files likely touched

- `src/DoipSimulator.WebConsole/src/App.vue`
- `src/DoipSimulator.WebConsole/src/api/client.ts`
- `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`
- `src/DoipSimulator.WebConsole/package.json`

### API / data contract

消费：

```http
GET /api/health
GET /api/config
```

前端内部模型：

```ts
type DashboardState = {
  health: HealthResponse;
  configSummary: {
    vin: string;
    eid: string;
    gid: string;
    logicalAddress: string;
    doipUdpPort: number;
    doipTcpPort: number;
    doipTlsPort: number;
  };
};
```

### Acceptance Criteria

- 打开启动 URL 能看到控制台。
- 服务状态和配置摘要正确展示。
- 后端不可用时页面显示错误状态。
- 页面刷新后数据仍正确。

### Test Plan

- 前端单元测试：状态组件渲染。
- 前端 API mock 测试：成功和失败状态。
- 手工测试：启动服务后浏览器访问 URL。

## Task 06：结构化事件模型和文件日志

### Goal

建立所有模块共用的结构化事件和文件日志通道。

### Scope

- 定义 `RuntimeEvent`。
- 实现事件发布接口。
- 实现文件日志异步写入。
- 支持事件分类和等级。
- 启动、停止、配置加载、配置保存写入事件。

### Out of Scope

- 不实现 Web 实时推送。
- 不实现高吞吐优化。
- 不实现日志查询。
- 不实现 pcap。

### Files likely touched

- `src/DoipSimulator.Core/Events/RuntimeEvent.cs`
- `src/DoipSimulator.Core/Events/IEventBus.cs`
- `src/DoipSimulator.Observability/Logging/FileEventSink.cs`
- `src/DoipSimulator.Host/Program.cs`
- `tests/DoipSimulator.Observability.Tests/Logging/`

### API / data contract

```json
{
  "id": "evt_000001",
  "timestamp": "2026-05-15T14:00:00.000Z",
  "level": "info",
  "category": "system",
  "name": "runtime.started",
  "message": "Simulator started.",
  "connectionId": null,
  "data": {}
}
```

事件分类初始枚举：

```text
system, config, connection, doip, uds, state, fault, tls, pcap
```

### Acceptance Criteria

- 启动和停止事件写入日志文件。
- 配置加载和保存事件写入日志文件。
- 日志文件为 UTF-8。
- 日志写入失败不导致主进程崩溃，但要有降级错误。

### Test Plan

- 单元测试：事件序列化字段完整。
- 单元测试：文件 sink 写入多条事件。
- 集成测试：启动服务后日志文件包含 `runtime.started`。

## Task 07：实时事件流 API 和控制台日志视图

### Goal

让 Web 控制台能实时看到运行事件，形成早期可观测闭环。

### Scope

- 提供内部事件流 API。
- 前端订阅事件流。
- 实现日志列表、等级过滤、分类过滤。
- 实现内存环形缓冲，避免 UI 无限增长。

### Out of Scope

- 不实现历史日志搜索。
- 不实现复杂图表。
- 不保证 100M 吞吐。

### Files likely touched

- `src/DoipSimulator.WebApi/Controllers/EventsController.cs`
- `src/DoipSimulator.WebApi/EventStreamHub.cs`
- `src/DoipSimulator.Observability/Events/InMemoryEventBuffer.cs`
- `src/DoipSimulator.WebConsole/src/views/LogsView.vue`
- `src/DoipSimulator.WebConsole/src/stores/events.ts`
- `tests/DoipSimulator.WebApi.Tests/EventStreamTests.cs`

### API / data contract

建议使用 WebSocket，内部 API：

```text
WS /api/events/stream
```

服务端推送 `RuntimeEvent` JSON。

可选历史读取：

```http
GET /api/events/recent?limit=200&category=doip
```

### Acceptance Criteria

- 页面打开后能看到启动事件。
- 新配置保存后日志视图实时出现配置事件。
- 断开后重连不会导致服务异常。
- UI 最多保留配置数量的事件，例如 1000 条。

### Test Plan

- API 测试：recent 事件返回最新 N 条。
- WebSocket 测试：发布事件后客户端收到。
- 前端测试：过滤器只显示指定分类。

## Task 08：DoIP 帧编解码核心

### Goal

实现独立可测试的 DoIP header/payload 编解码，不依赖 socket。

### Scope

- 定义 DoIP payload type 枚举。
- 实现 DoIP header 解码和编码。
- 校验 protocol version、inverse version、payload length。
- 支持错误模型。
- 支持后续 payload 解析的基础类型。

### Out of Scope

- 不实现 UDP/TCP socket。
- 不实现路由激活业务。
- 不实现 UDS 服务。

### Files likely touched

- `src/DoipSimulator.Protocols.Doip/DoipFrame.cs`
- `src/DoipSimulator.Protocols.Doip/DoipPayloadType.cs`
- `src/DoipSimulator.Protocols.Doip/DoipCodec.cs`
- `src/DoipSimulator.Protocols.Doip/DoipProtocolError.cs`
- `tests/DoipSimulator.Protocols.Doip.Tests/DoipCodecTests.cs`

### API / data contract

```csharp
public sealed record DoipFrame(
    byte ProtocolVersion,
    byte InverseProtocolVersion,
    ushort PayloadType,
    uint PayloadLength,
    byte[] Payload);

public interface IDoipCodec
{
    DecodeResult<DoipFrame> Decode(ReadOnlySpan<byte> bytes);
    byte[] Encode(DoipFrame frame);
}
```

### Acceptance Criteria

- 正确编码/解码合法 DoIP frame。
- inverse version 错误返回明确错误。
- payload length 与实际长度不一致返回明确错误。
- codec 测试不需要启动网络服务。

### Test Plan

- 单元测试：合法 header round-trip。
- 单元测试：inverse version 错误。
- 单元测试：length 过短/过长。
- 单元测试：未知 payload type 可被保留并上报。

## Task 09：UDP 车辆发现和公告

### Goal

让诊断客户端可以通过 UDP 发现模拟 ECU。

### Scope

- UDP 监听 DoIP 端口。
- 支持 Vehicle Identification Request/Response。
- 支持 Vehicle Announcement 定时广播或按配置发送。
- 响应内容来自 `SimulatorConfig.entity`。
- DoIP 帧事件写入日志和 UI。

### Out of Scope

- 不实现 TCP。
- 不实现路由激活。
- 不实现 TLS。

### Files likely touched

- `src/DoipSimulator.Transport/Udp/UdpDoipServer.cs`
- `src/DoipSimulator.Protocols.Doip/VehicleIdentificationHandler.cs`
- `src/DoipSimulator.Protocols.Doip/DoipEntityInfo.cs`
- `src/DoipSimulator.Host/RuntimeOrchestrator.cs`
- `tests/DoipSimulator.Protocols.Doip.Tests/VehicleIdentificationTests.cs`
- `tests/DoipSimulator.Transport.Tests/UdpDoipServerTests.cs`

### API / data contract

内部处理接口：

```csharp
public interface IDoipUdpHandler
{
    ValueTask<IReadOnlyList<OutboundDatagram>> HandleAsync(
        InboundDatagram datagram,
        CancellationToken cancellationToken);
}
```

事件：

```json
{
  "category": "doip",
  "name": "doip.udp.vehicle_identification.responded",
  "data": {
    "vin": "LTEST000000000001",
    "logicalAddress": "0x0E00",
    "remoteEndpoint": "192.168.1.10:50000"
  }
}
```

### Acceptance Criteria

- UDP 服务随 Host 启动。
- 收到车辆识别请求后返回车辆识别响应。
- Vehicle Announcement 可按配置发送。
- Web 日志能看到请求和响应摘要。

### Test Plan

- 单元测试：车辆识别响应 payload 字段正确。
- 集成测试：本地 UDP client 发送请求并收到响应。
- 手工测试：内部诊断上位机能发现 ECU。

## Task 10：TCP 连接管理和路由激活

### Goal

实现 DoIP TCP 连接与路由激活，建立诊断通信前置链路。

### Scope

- TCP 监听 DoIP 端口。
- 连接创建、断开、超时事件。
- TCP 粘包/半包 frame 组包。
- 路由激活请求/响应。
- 源地址白名单校验。
- Alive Check 基础支持。

### Out of Scope

- 不处理 UDS 业务响应。
- 不实现 TLS。
- 不实现复杂异常注入。

### Files likely touched

- `src/DoipSimulator.Transport/Tcp/TcpDoipServer.cs`
- `src/DoipSimulator.Transport/Tcp/DoipStreamReader.cs`
- `src/DoipSimulator.Protocols.Doip/RoutingActivationHandler.cs`
- `src/DoipSimulator.Protocols.Doip/AliveCheckHandler.cs`
- `src/DoipSimulator.Core/Connections/ConnectionRegistry.cs`
- `tests/DoipSimulator.Transport.Tests/Tcp/`
- `tests/DoipSimulator.Protocols.Doip.Tests/RoutingActivationTests.cs`

### API / data contract

连接快照：

```json
{
  "connectionId": "conn_000001",
  "transport": "tcp",
  "remoteEndpoint": "192.168.1.10:50001",
  "routingActivated": true,
  "testerLogicalAddress": "0x0E80",
  "ecuLogicalAddress": "0x0E00",
  "connectedAt": "2026-05-15T14:00:00Z"
}
```

### Acceptance Criteria

- 客户端可建立 TCP 连接。
- 合法源地址路由激活成功。
- 非白名单源地址路由激活失败。
- 半包/粘包情况下 frame 解析正确。
- 断开后连接状态从 registry 移除或标记断开。

### Test Plan

- 单元测试：路由激活成功/失败响应。
- 单元测试：stream reader 处理半包和粘包。
- 集成测试：TCP client 完成路由激活。
- 手工测试：诊断上位机完成路由激活。

## Task 11：UDS 分发框架和 NRC 响应模型

### Goal

建立 UDS 服务分发基础，让后续服务可以按小任务逐步接入。

### Scope

- 定义 `UdsRequest`、`UdsResponse`、`NegativeResponse`。
- 实现服务分发器。
- 实现基础 NRC。
- DoIP 诊断消息 payload 转发到 UDS dispatcher。
- 未支持服务返回 `0x7F SID 0x11`。

### Out of Scope

- 不实现具体 UDS 服务正响应。
- 不实现 ECU 状态机细节。
- 不实现 SecurityAccess。

### Files likely touched

- `src/DoipSimulator.Protocols.Uds/UdsRequest.cs`
- `src/DoipSimulator.Protocols.Uds/UdsResponse.cs`
- `src/DoipSimulator.Protocols.Uds/UdsDispatcher.cs`
- `src/DoipSimulator.Protocols.Uds/Nrc.cs`
- `src/DoipSimulator.Protocols.Doip/DiagnosticMessageHandler.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/UdsDispatcherTests.cs`

### API / data contract

```csharp
public sealed record UdsRequest(byte ServiceId, byte[] Payload);

public abstract record UdsResponse
{
    public abstract byte[] ToBytes();
}

public interface IUdsService
{
    byte ServiceId { get; }
    ValueTask<IReadOnlyList<UdsResponse>> HandleAsync(
        UdsRequest request,
        UdsContext context,
        CancellationToken cancellationToken);
}
```

### Acceptance Criteria

- DoIP diagnostic message 能进入 UDS dispatcher。
- 未支持 SID 返回正确 NRC。
- 请求长度错误可返回 `0x13`。
- UDS 请求/响应事件写入日志。

### Test Plan

- 单元测试：未知服务返回 `7F xx 11`。
- 单元测试：服务处理器可注册和分发。
- 集成测试：TCP 路由激活后发送未知 UDS 服务收到 NRC。

## Task 12：最小 ECU 状态和 `0x10`/`0x3E`

### Goal

实现诊断会话和 TesterPresent 的最小可用状态机。

### Scope

- 定义 ECU runtime state。
- 实现 `0x10` DiagnosticSessionControl。
- 实现 `0x3E` TesterPresent。
- 会话状态变化写入事件。
- 返回 P2/P2* 基础参数。

### Out of Scope

- 不实现 TesterPresent 超时回退。
- 不实现 SecurityAccess。
- 不实现 ResponsePending。

### Files likely touched

- `src/DoipSimulator.Core/Ecu/EcuRuntimeState.cs`
- `src/DoipSimulator.Core/Ecu/SessionState.cs`
- `src/DoipSimulator.Protocols.Uds/Services/DiagnosticSessionControlService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/TesterPresentService.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/`

### API / data contract

状态快照：

```json
{
  "logicalAddress": "0x0E00",
  "session": "extended",
  "securityLevel": "locked",
  "lastTesterPresentAt": "2026-05-15T14:00:00Z"
}
```

### Acceptance Criteria

- `10 01` 切换默认会话。
- `10 03` 切换扩展会话。
- `10 02` 切换编程会话。
- `3E 00` 返回正响应。
- 会话变化可在事件中看到。

### Test Plan

- 单元测试：各会话子功能正响应。
- 单元测试：未知会话子功能返回 NRC。
- 集成测试：路由激活后请求 `0x10` 和 `0x3E`。

## Task 13：DID 配置和 `0x22` 读取

### Goal

实现最小数据读取能力，让诊断客户端能读取配置中的 DID。

### Scope

- 扩展 DID 配置模型。
- 实现 `0x22` ReadDataByIdentifier。
- 支持一个请求读取多个 DID。
- 支持固定字节值。
- 未配置 DID 返回 NRC。

### Out of Scope

- 不支持动态表达式 DID。
- 不支持写 DID。
- 不支持 ODX 导入。

### Files likely touched

- `src/DoipSimulator.Core/Configuration/UdsDidConfig.cs`
- `src/DoipSimulator.Protocols.Uds/Services/ReadDataByIdentifierService.cs`
- `sample-config/default.simulator.json`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/ReadDataByIdentifierTests.cs`

### API / data contract

DID 配置：

```json
{
  "id": "0xF190",
  "name": "VIN",
  "valueEncoding": "hex",
  "value": "4C54455354303030303030303030303031",
  "read": {
    "allowedSessions": ["default", "extended", "programming"],
    "securityRequired": false
  }
}
```

### Acceptance Criteria

- `22 F1 90` 返回 `62 F1 90 ...`。
- 多 DID 请求按请求顺序返回。
- 未配置 DID 返回 `0x31 RequestOutOfRange`。
- DID 读取事件包含 DID ID 和响应长度。

### Test Plan

- 单元测试：单 DID、多 DID、未知 DID。
- 单元测试：请求长度奇数返回 `0x13`。
- 集成测试：真实 TCP DoIP 链路读取 DID。

## Task 14：连接、DoIP、UDS 实时观测 UI

### Goal

让控制台能实时观察连接、DoIP 帧、UDS 报文和 ECU 状态。

### Scope

- 增加连接列表。
- 增加 DoIP 报文列表。
- 增加 UDS 报文列表。
- 增加 ECU 状态面板。
- 支持基础过滤。

### Out of Scope

- 不实现报文重放。
- 不实现图表分析。
- 不实现 pcap 下载。

### Files likely touched

- `src/DoipSimulator.WebApi/Controllers/ConnectionsController.cs`
- `src/DoipSimulator.WebApi/Controllers/EcuStateController.cs`
- `src/DoipSimulator.WebConsole/src/views/DiagnosticsView.vue`
- `src/DoipSimulator.WebConsole/src/components/ConnectionTable.vue`
- `src/DoipSimulator.WebConsole/src/components/MessageTraceTable.vue`
- `src/DoipSimulator.WebConsole/src/components/EcuStatePanel.vue`

### API / data contract

```http
GET /api/connections
GET /api/ecu/state
```

WebSocket 继续复用 `RuntimeEvent`，新增事件名：

```text
connection.opened
connection.closed
doip.frame.received
doip.frame.sent
uds.request.received
uds.response.sent
state.session.changed
```

### Acceptance Criteria

- 客户端连接后 UI 显示连接。
- 发送 DoIP/UDS 请求后 UI 显示请求和响应。
- 会话切换后 UI 状态实时更新。
- 断开连接后 UI 显示连接关闭。

### Test Plan

- 前端测试：连接表、报文表渲染。
- API 测试：连接快照返回结构。
- 手工测试：诊断上位机连接并观察 UI。

## Task 15：DID 运行时编辑和 `0x2E` 写入

### Goal

支持 Web 修改 DID 值和 UDS 写 DID，使数据配置形成运行时闭环。

### Scope

- Web 控制台 DID 列表和编辑。
- 内部 API 修改 DID runtime value。
- 实现 `0x2E` WriteDataByIdentifier。
- 支持写入权限：会话和安全前置条件。
- 保存 DID 配置到 JSON。

### Out of Scope

- 不支持复杂编码转换。
- 不支持 ODX 写入定义。
- 不支持动态 DID。

### Files likely touched

- `src/DoipSimulator.WebApi/Controllers/DidsController.cs`
- `src/DoipSimulator.Core/Ecu/DidRuntimeStore.cs`
- `src/DoipSimulator.Protocols.Uds/Services/WriteDataByIdentifierService.cs`
- `src/DoipSimulator.WebConsole/src/views/Config/DidsView.vue`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/WriteDataByIdentifierTests.cs`

### API / data contract

```http
GET /api/dids
PUT /api/dids/{did}/value
```

```json
{
  "valueEncoding": "hex",
  "value": "01020304",
  "persist": true
}
```

### Acceptance Criteria

- Web 修改 DID 后 `0x22` 立即读到新值。
- `0x2E` 写入 DID 后 Web 显示新值。
- 不允许写入的 DID 返回正确 NRC。
- 持久化后重启仍保留新值。

### Test Plan

- 单元测试：写 DID 成功、禁止写、长度错误。
- API 测试：PUT DID value 后 GET 返回更新值。
- 集成测试：`0x2E` 后 `0x22` 验证。

## Task 16：DTC 服务 `0x19`/`0x14` 和 Web 注入

### Goal

实现基础 DTC 查询、清除和 Web 注入能力。

### Scope

- DTC 配置和 runtime store。
- Web 注入、激活、清除 DTC。
- 实现 `0x19` 的 MVP 子集。
- 实现 `0x14` 清除 DTC。
- DTC 状态变化事件。

### Out of Scope

- 不覆盖 `0x19` 全部子功能。
- 不实现真实老化、确认、测试失败完整状态机。
- 不导入 ODX DTC。

### Files likely touched

- `src/DoipSimulator.Core/Ecu/DtcRuntimeStore.cs`
- `src/DoipSimulator.Core/Configuration/UdsDtcConfig.cs`
- `src/DoipSimulator.Protocols.Uds/Services/ReadDtcInformationService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/ClearDiagnosticInformationService.cs`
- `src/DoipSimulator.WebApi/Controllers/DtcsController.cs`
- `src/DoipSimulator.WebConsole/src/views/Diagnostics/DtcsView.vue`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/DtcServiceTests.cs`

### API / data contract

DTC runtime：

```json
{
  "code": "0x123456",
  "status": "0x2F",
  "active": true,
  "description": "Sample DTC"
}
```

```http
GET /api/dtcs
POST /api/dtcs/{code}/activate
POST /api/dtcs/{code}/clear
```

### Acceptance Criteria

- Web 激活 DTC 后 `0x19` 可读取。
- `0x14` 清除后 Web 和 `0x19` 均反映清除结果。
- 未知 DTC 操作返回明确错误。
- DTC 事件进入日志。

### Test Plan

- 单元测试：DTC 激活、清除、查询。
- API 测试：Web 注入接口。
- 集成测试：激活后通过 DoIP/UDS 查询。

## Task 17：Routine、通信控制和 DTC 设置基础服务

### Goal

覆盖 MVP 中常见控制类 UDS 服务的基础路径。

### Scope

- 实现 `0x31` RoutineControl 固定响应。
- 实现 `0x28` CommunicationControl 基础状态切换。
- 实现 `0x85` ControlDTCSetting 基础状态切换。
- Web 展示 Routine 配置和控制状态。

### Out of Scope

- 不实现复杂 Routine 执行脚本。
- 不实现真实通信通道关闭。
- 不实现完整 DTC setting 细分行为。

### Files likely touched

- `src/DoipSimulator.Core/Configuration/UdsRoutineConfig.cs`
- `src/DoipSimulator.Protocols.Uds/Services/RoutineControlService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/CommunicationControlService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/ControlDtcSettingService.cs`
- `src/DoipSimulator.WebConsole/src/views/Diagnostics/RoutinesView.vue`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/ControlServicesTests.cs`

### API / data contract

Routine 配置：

```json
{
  "routineId": "0x0201",
  "name": "EraseMemory",
  "allowedSessions": ["programming"],
  "securityRequired": true,
  "responses": {
    "start": "020100",
    "stop": "020100",
    "requestResults": "020100"
  }
}
```

### Acceptance Criteria

- 配置内 Routine 可通过 `0x31` 调用并返回固定响应。
- 非法 Routine ID 返回 NRC。
- `0x28` 改变通信控制状态并产生事件。
- `0x85` 改变 DTC 设置状态并产生事件。

### Test Plan

- 单元测试：Routine start/stop/result。
- 单元测试：会话或安全条件不满足返回 NRC。
- 集成测试：通过 DoIP 调用 `0x31/0x28/0x85`。

## Task 18：SecurityAccess 内置算法和安全状态

### Goal

在 DLL 插件之前先实现可测试的 SecurityAccess 主路径。

### Scope

- SecurityAccess 配置模型。
- 实现 `0x27` seed/key。
- 内置示例算法，例如 XOR 或加法算法。
- 维护安全等级解锁状态。
- 错误 key 次数和锁定时间的基础配置。

### Out of Scope

- 不加载 DLL。
- 不实现 OEM 真实算法。
- 不实现 `0x84`。

### Files likely touched

- `src/DoipSimulator.Core/Configuration/SecurityAccessConfig.cs`
- `src/DoipSimulator.Core/Ecu/SecurityState.cs`
- `src/DoipSimulator.Protocols.Uds/Services/SecurityAccessService.cs`
- `src/DoipSimulator.Security/BuiltinSecurityAlgorithm.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/SecurityAccessTests.cs`

### API / data contract

```json
{
  "level": 1,
  "seedRequestSubFunction": "0x01",
  "keySendSubFunction": "0x02",
  "algorithm": {
    "type": "builtin-xor",
    "parameter": "A5A5A5A5"
  },
  "maxFailedAttempts": 3,
  "lockoutMs": 10000
}
```

### Acceptance Criteria

- 请求 seed 返回非空 seed。
- 正确 key 解锁指定安全等级。
- 错误 key 返回 NRC，并累计失败次数。
- 达到失败次数后进入锁定状态。
- 解锁状态影响受保护 DID/Routine。

### Test Plan

- 单元测试：seed/key 正常流程。
- 单元测试：错误 key 和锁定。
- 集成测试：未解锁读取受保护 DID 失败，解锁后成功。

## Task 19：P2/P2*、TesterPresent 超时和 ResponsePending

### Goal

增强状态机真实性，支持诊断客户端常见时序行为验证。

### Scope

- TesterPresent 超时回退默认会话。
- P2/P2* 配置进入会话响应。
- 支持服务级响应延迟。
- 支持 `0x78 ResponsePending` 后最终响应。
- Web 展示定时状态。

### Out of Scope

- 不实现复杂调度器。
- 不实现概率型延迟。
- 不实现全部 OEM 时序策略。

### Files likely touched

- `src/DoipSimulator.Core/Ecu/EcuRuntimeState.cs`
- `src/DoipSimulator.Core/Ecu/EcuTimers.cs`
- `src/DoipSimulator.Protocols.Uds/UdsResponseScheduler.cs`
- `src/DoipSimulator.Protocols.Uds/Services/DiagnosticSessionControlService.cs`
- `src/DoipSimulator.WebConsole/src/components/EcuStatePanel.vue`
- `tests/DoipSimulator.Protocols.Uds.Tests/Timing/`

### API / data contract

服务响应策略：

```json
{
  "serviceId": "0x31",
  "responsePending": {
    "enabled": true,
    "initialDelayMs": 50,
    "finalDelayMs": 1000
  }
}
```

### Acceptance Criteria

- TesterPresent 超时后会话回退并产生日志。
- `0x10` 响应包含配置的 P2/P2*。
- 配置 ResponsePending 后先返回 `7F SID 78`，再返回最终响应。
- 定时行为不阻塞其他连接的基础处理。

### Test Plan

- 单元测试：超时回退。
- 单元测试：ResponsePending 响应序列。
- 集成测试：客户端收到 `0x78` 和最终响应。

## Task 20：Flash 下载主流程 `0x34-0x37`

### Goal

支持刷写流程主路径，用于诊断上位机流程开发验证。

### Scope

- Flash 配置模型。
- 实现 `0x34` RequestDownload。
- 实现 `0x36` TransferData。
- 实现 `0x37` RequestTransferExit。
- 可选实现 `0x35` RequestUpload 的简化路径。
- 维护下载状态、总大小、块大小、块序号。

### Out of Scope

- 不做真实文件写入。
- 不做签名验签。
- 不做完整内存地址映射。
- 不做刷写后 ECU reset 联动。

### Files likely touched

- `src/DoipSimulator.Core/Configuration/FlashConfig.cs`
- `src/DoipSimulator.Core/Ecu/FlashTransferState.cs`
- `src/DoipSimulator.Protocols.Uds/Services/RequestDownloadService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/TransferDataService.cs`
- `src/DoipSimulator.Protocols.Uds/Services/RequestTransferExitService.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/Services/FlashTransferTests.cs`

### API / data contract

```json
{
  "flash": {
    "enabled": true,
    "maxMemorySize": 1048576,
    "maxBlockLength": 4096,
    "allowedSessions": ["programming"],
    "securityRequired": true
  }
}
```

### Acceptance Criteria

- `0x34 -> 0x36*N -> 0x37` 正常完成。
- 未进入编程会话返回 NRC。
- 未解锁返回 NRC。
- 块序号错误返回 NRC。
- 中途断连后下载状态清理或进入可恢复安全状态。

### Test Plan

- 单元测试：正常下载流程。
- 单元测试：会话、安全、块序号异常。
- 集成测试：通过 DoIP TCP 跑完整刷写主路径。

## Task 21：PCAP 录制 MVP

### Goal

生成可被 Wireshark 打开的 pcap 文件，用于问题定位。

### Scope

- 实现 pcap writer。
- 记录 TCP/UDP DoIP 收发数据。
- 支持开始、停止、状态查询。
- 支持 500M 文件大小上限。
- Web 显示录制状态。

### Out of Scope

- 不保证 TLS 内容解密。
- 不做 pcapng 高级元数据。
- 不做报文索引搜索。

### Files likely touched

- `src/DoipSimulator.Observability/Pcap/PcapWriter.cs`
- `src/DoipSimulator.Observability/Pcap/PcapRecorder.cs`
- `src/DoipSimulator.WebApi/Controllers/PcapController.cs`
- `src/DoipSimulator.WebConsole/src/views/PcapView.vue`
- `tests/DoipSimulator.Observability.Tests/Pcap/`

### API / data contract

```http
GET /api/pcap/status
POST /api/pcap/start
POST /api/pcap/stop
```

```json
{
  "recording": true,
  "filePath": "logs/pcap/session-20260515-140000.pcap",
  "bytesWritten": 1048576,
  "maxBytes": 524288000
}
```

### Acceptance Criteria

- 开启录制后生成 pcap 文件。
- 执行 UDP 发现和 TCP UDS 请求后 pcap 非空。
- Wireshark 可打开文件。
- 达到大小上限后停止或轮转，并产生事件。

### Test Plan

- 单元测试：pcap global header 和 packet header 写入正确。
- 集成测试：录制一次 UDP/TCP 交互。
- 手工测试：用 Wireshark 打开生成文件。

## Task 22：TLS 传输和证书配置

### Goal

支持 DoIP over TLS 主路径和证书错误可观测。

### Scope

- TLS 监听端口。
- 服务端证书加载。
- 客户端证书校验。
- 双向认证配置。
- TLS 连接事件和错误事件。
- TLS 下复用 DoIP routing activation 和 UDS dispatcher。

### Out of Scope

- 不实现证书生成 UI。
- 不默认解密 pcap 中 TLS 内容。
- 不实现 `0x84`。

### Files likely touched

- `src/DoipSimulator.Transport/Tls/TlsDoipServer.cs`
- `src/DoipSimulator.Security/Tls/CertificateLoader.cs`
- `src/DoipSimulator.Security/Tls/ClientCertificateValidator.cs`
- `src/DoipSimulator.Core/Configuration/TlsConfig.cs`
- `tests/DoipSimulator.Transport.Tests/Tls/`

### API / data contract

TLS 配置沿用：

```json
{
  "tls": {
    "enabled": true,
    "serverCertificatePath": "certs/server.pfx",
    "serverCertificatePassword": "changeit",
    "clientCaPath": "certs/client-ca.pem",
    "requireClientCertificate": true,
    "simulateCertificateError": null
  }
}
```

### Acceptance Criteria

- 合法客户端证书可建立 TLS 连接。
- TLS 下可完成路由激活和 `0x10/0x22/0x3E`。
- 非法证书连接失败，日志包含错误原因。
- Web UI 能区分 TCP 和 TLS 连接。

### Test Plan

- 单元测试：证书配置缺失时错误明确。
- 集成测试：自签证书 mTLS 成功。
- 集成测试：无客户端证书失败。
- 手工测试：诊断上位机使用 TLS 连接。

## Task 23：DLL 安全算法插件 MVP

### Goal

建立 SecurityAccess 的 DLL 插件边界，并提供示例插件。

### Scope

- 定义 DLL ABI 文档。
- 实现插件加载和版本检查。
- 用插件替代内置 `0x27` 算法。
- 提供示例 DLL 工程。
- 处理插件缺失、版本不匹配、调用失败。

### Out of Scope

- 不实现进程隔离。
- 不支持脚本插件。
- 不实现企业真实 OEM 算法。
- 不完整实现 `0x84` 加解密。

### Files likely touched

- `src/DoipSimulator.Security/Plugins/SecurityPluginLoader.cs`
- `src/DoipSimulator.Security/Plugins/ISecurityAlgorithm.cs`
- `src/DoipSimulator.Core/Configuration/SecurityPluginConfig.cs`
- `samples/SecurityPluginExample/`
- `docs/SecurityPlugin-ABI.md`
- `tests/DoipSimulator.Security.Tests/Plugins/`

### API / data contract

DLL 导出函数建议：

```c
int DoipSec_GetAbiVersion(void);
int DoipSec_GenerateSeed(
  int level,
  const unsigned char* context,
  int contextLength,
  unsigned char* seedOut,
  int* seedLength);
int DoipSec_VerifyKey(
  int level,
  const unsigned char* seed,
  int seedLength,
  const unsigned char* key,
  int keyLength);
```

配置：

```json
{
  "securityPlugin": {
    "enabled": true,
    "dllPath": "plugins/SampleSecurityPlugin.dll",
    "timeoutMs": 500
  }
}
```

### Acceptance Criteria

- 示例 DLL 可加载。
- `0x27` seed 来自 DLL。
- 正确 key 解锁成功。
- 错误 key 返回 NRC。
- DLL 缺失或 ABI 不匹配时错误清晰，服务不崩溃。

### Test Plan

- 单元测试：插件路径不存在。
- 单元测试：ABI 版本不匹配。
- 集成测试：示例插件完成 seed/key。
- 手工测试：替换插件路径后观察 Web 日志。

## Task 24：异常注入第一批

### Goal

提供可复现的第一批异常场景，用于诊断上位机鲁棒性验证。

### Scope

- Fault profile 配置模型。
- Web 切换异常策略。
- 响应延迟。
- 暂停/恢复响应。
- TCP 主动断开。
- 路由激活失败。
- 错误 inverse version。
- 错误 payload length。
- 手动 NRC 和自定义 UDS 响应。

### Out of Scope

- 不做复杂乱序。
- 不做概率型策略编排。
- 不做所有 TLS 失败组合。
- 不做长期故障脚本系统。

### Files likely touched

- `src/DoipSimulator.Core/Faults/FaultProfile.cs`
- `src/DoipSimulator.Core/Faults/FaultRuntimeState.cs`
- `src/DoipSimulator.Protocols.Doip/FaultableDoipResponder.cs`
- `src/DoipSimulator.Protocols.Uds/FaultableUdsResponder.cs`
- `src/DoipSimulator.WebApi/Controllers/FaultsController.cs`
- `src/DoipSimulator.WebConsole/src/views/FaultInjectionView.vue`
- `tests/DoipSimulator.Core.Tests/Faults/`

### API / data contract

```http
GET /api/faults
PUT /api/faults
POST /api/faults/actions/disconnect
POST /api/faults/actions/next-nrc
```

```json
{
  "enabled": true,
  "responseDelayMs": 1000,
  "pauseResponses": false,
  "routingActivationFailure": false,
  "corruptNextDoipHeader": {
    "inverseVersion": true,
    "payloadLengthDelta": 1
  }
}
```

### Acceptance Criteria

- 开启响应延迟后客户端感知延迟。
- 暂停响应后客户端超时。
- 手动断开使目标连接关闭。
- 路由激活失败可复现。
- 下一次指定服务可被手动 NRC 覆盖。

### Test Plan

- 单元测试：fault profile 校验。
- 集成测试：延迟、暂停、断连。
- 集成测试：错误 DoIP header。
- 手工测试：通过 Web 操作并观察诊断上位机行为。

## Task 25：ODX/PDX 导入子集

### Goal

打通 ODX/PDX 到内部配置模型的第一条路径。

### Scope

- 支持上传 `.odx`。
- 支持上传 `.pdx` 并解压识别入口。
- 解析 ECU 基本信息和 DID 基础信息。
- 生成导入报告。
- 导入结果可合并并保存到 `SimulatorConfig`。

### Out of Scope

- 不解析全量 ODX。
- 不完整解析 DTC、Routine、Flash。
- 不做第三方工具链兼容认证。

### Files likely touched

- `src/DoipSimulator.Core/Odx/OdxImportService.cs`
- `src/DoipSimulator.Core/Odx/PdxPackageReader.cs`
- `src/DoipSimulator.Core/Odx/OdxImportReport.cs`
- `src/DoipSimulator.WebApi/Controllers/ImportController.cs`
- `src/DoipSimulator.WebConsole/src/views/ImportView.vue`
- `tests/DoipSimulator.Core.Tests/Odx/`
- `tests/fixtures/odx/`

### API / data contract

```http
POST /api/import/odx
POST /api/import/pdx
```

导入报告：

```json
{
  "success": true,
  "imported": {
    "entityInfo": true,
    "dids": 12,
    "dtcs": 0,
    "routines": 0
  },
  "skipped": [
    { "path": "/ODX/FLASH", "reason": "Not supported in MVP import subset." }
  ],
  "errors": []
}
```

### Acceptance Criteria

- 样例 `.odx` 可导入 DID。
- 样例 `.pdx` 可解压并导入 ECU 基本信息。
- 不支持字段进入 skipped 列表。
- 错误文件返回失败报告，不导致服务崩溃。
- 导入结果可保存并被 `0x22` 读取。

### Test Plan

- 单元测试：ODX 样例解析。
- 单元测试：PDX 解压和入口识别。
- API 测试：上传错误文件。
- 集成测试：导入 DID 后通过 UDS 读取。

## Task 26：性能指标、资源治理和 MVP 验收脚本

### Goal

为 MVP 提供可重复的性能验证和基础资源治理能力。

### Scope

- 采集连接数、RPS、队列长度、日志写入速率、pcap 写入速率、内存快照。
- Web 显示基础运行指标。
- 增加简单压测工具或测试脚本。
- 验证 20 并发连接、200 请求/秒。
- 增加长稳运行检查项文档。

### Out of Scope

- 不做企业级监控系统。
- 不做分布式压测。
- 不新增大功能。
- 不做大规模重构。

### Files likely touched

- `src/DoipSimulator.Observability/Metrics/RuntimeMetrics.cs`
- `src/DoipSimulator.Observability/Metrics/MetricsCollector.cs`
- `src/DoipSimulator.WebApi/Controllers/MetricsController.cs`
- `src/DoipSimulator.WebConsole/src/views/MetricsView.vue`
- `tools/loadtest/`
- `docs/MVP-Acceptance-Test-Plan.md`
- `tests/DoipSimulator.IntegrationTests/`

### API / data contract

```http
GET /api/metrics
```

```json
{
  "connections": {
    "active": 20,
    "totalAccepted": 100
  },
  "throughput": {
    "udsRequestsPerSecond": 200,
    "eventsPerSecond": 500
  },
  "queues": {
    "eventQueueLength": 120,
    "pcapQueueLength": 20
  },
  "process": {
    "workingSetBytes": 268435456
  }
}
```

### Acceptance Criteria

- `/api/metrics` 返回运行指标。
- 20 并发 TCP 连接可维持。
- 200 RPS 下核心 UDS 请求响应正确率达标。
- 日志和 pcap 同时开启时核心协议处理不被 UI 阻塞。
- 形成 MVP 验收测试文档。

### Test Plan

- 单元测试：metrics 快照聚合。
- 集成测试：多连接短时压测。
- 手工测试：运行压测脚本并观察 Web 指标。
- 长稳测试：按文档执行 1 天运行验证。

## 建议执行顺序

| 顺序 | Task | 依赖 |
| --- | --- | --- |
| 1 | Task 01 | 无 |
| 2 | Task 02 | Task 01 |
| 3 | Task 03 | Task 01 |
| 4 | Task 04 | Task 02, Task 03 |
| 5 | Task 05 | Task 02, Task 04 |
| 6 | Task 06 | Task 03 |
| 7 | Task 07 | Task 05, Task 06 |
| 8 | Task 08 | Task 03 |
| 9 | Task 09 | Task 06, Task 08 |
| 10 | Task 10 | Task 08, Task 09 |
| 11 | Task 11 | Task 08, Task 10 |
| 12 | Task 12 | Task 11 |
| 13 | Task 13 | Task 03, Task 11, Task 12 |
| 14 | Task 14 | Task 07, Task 10, Task 13 |
| 15 | Task 15 | Task 13, Task 14 |
| 16 | Task 16 | Task 11, Task 14 |
| 17 | Task 17 | Task 12, Task 16 |
| 18 | Task 18 | Task 12, Task 13, Task 17 |
| 19 | Task 19 | Task 12, Task 18 |
| 20 | Task 20 | Task 18, Task 19 |
| 21 | Task 21 | Task 09, Task 10 |
| 22 | Task 22 | Task 10, Task 21 |
| 23 | Task 23 | Task 18 |
| 24 | Task 24 | Task 10, Task 11, Task 14 |
| 25 | Task 25 | Task 03, Task 13 |
| 26 | Task 26 | Task 14, Task 21, Task 22 |

## Agent 执行提示模板

后续可以把单个 Task 直接交给 AI coding agent，建议使用以下提示格式：

```text
请实现 MVP-Task-Specs.md 中的 Task XX。
要求：
- 只修改该 Task 的 Files likely touched 或必要的邻近文件。
- 不扩大 Scope。
- 保持现有测试通过。
- 为新增逻辑补充测试。
- 完成后说明改动、测试结果、未完成项。
```
