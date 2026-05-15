# MVP 开发路线：Windows DoIP ECU 模拟器

本文基于 `Product-Brief.md` 和 `System-Modules.md`，目标是用小步提交逐步形成可运行闭环。路线原则：

- 优先完成端到端可运行链路，再逐步增加协议深度。
- 每一步都能启动、测试、回归。
- 每一步都可以作为独立 commit。
- 避免先做大而全的抽象，模块边界先稳定，内部实现逐步加深。

## MVP 最小闭环定义

MVP 的最小闭环不是“完整功能”，而是：

1. 命令行启动服务。
2. 控制台输出 Web URL。
3. 浏览器进入 Web 控制台。
4. DoIP 客户端能发现模拟 ECU。
5. DoIP 客户端能建立 TCP 连接并完成路由激活。
6. DoIP 客户端能发送 UDS 请求并收到响应。
7. Web 控制台能看到连接、报文和状态变化。
8. 文件日志和 pcap 能输出可用于定位问题的数据。

## 路线总览

| 步骤 | 目标 | 主要模块 | 可测试结果 | 建议提交 |
| --- | --- | --- | --- | --- |
| 1 | 建立工程骨架与启动入口 | 启动与运行时 | 命令行启动并输出 URL | `init runtime skeleton` |
| 2 | 建立配置模型与默认配置 | 配置与持久化 | 能加载/校验/保存 JSON 配置 | `add config model` |
| 3 | 建立 Web 控制台空壳 | Web 控制台、控制面 API | 浏览器可打开控制台并看到服务状态 | `add web console shell` |
| 4 | 建立事件与日志管道 | 日志与事件 | UI 和文件都能看到启动/配置事件 | `add event logging pipeline` |
| 5 | 实现 UDP 发现闭环 | DoIP 网络传输、DoIP 协议 | 诊断客户端能发现 ECU | `add doip udp discovery` |
| 6 | 实现 TCP 路由激活闭环 | DoIP 网络传输、DoIP 协议 | 客户端能 TCP 连接并路由激活 | `add doip tcp routing activation` |
| 7 | 实现 UDS 最小服务集 | UDS、ECU 状态机 | `0x10/0x3E/0x22` 可请求响应 | `add minimal uds services` |
| 8 | 接入 Web 实时观测 | Web 控制台、控制面 API、日志 | UI 可看连接、DoIP、UDS、状态 | `add realtime diagnostics view` |
| 9 | 增加 DID/DTC/Routine 配置闭环 | 配置、UDS、状态机 | Web 修改 DID，客户端读取生效 | `add configurable uds data` |
| 10 | 增加核心状态机与 NRC | ECU 状态机、UDS | 会话、安全、超时、NRC 可测试 | `add ecu state machine rules` |
| 11 | 增加刷写主流程 | UDS、状态机 | `0x34-0x37` 主路径可跑通 | `add flash transfer happy path` |
| 12 | 增加 pcap 录制 | PCAP、网络传输 | Wireshark 可打开 pcap | `add pcap recording` |
| 13 | 增加 TLS 双向认证 | TLS 与证书、网络传输 | TLS 连接和证书错误可测试 | `add doip tls transport` |
| 14 | 增加 DLL 安全算法插件 | 安全算法插件、UDS | `0x27` 可调用示例 DLL | `add security dll plugin` |
| 15 | 增加异常注入第一批 | 异常注入 | 延迟、断连、错误 header 可复现 | `add first fault injection set` |
| 16 | 增加 ODX/PDX 导入第一批 | ODX/PDX、配置 | 可导入 ECU 基本信息和 DID | `add odx pdx import subset` |
| 17 | MVP 压测与稳定性收敛 | 性能与资源治理 | 20 连接、200 RPS、1 天运行 | `stabilize mvp performance` |

## Step 1：工程骨架与启动入口

### 目标

建立可运行的服务进程。先不做协议，只完成启动、退出、端口配置和 URL 输出。

### 范围

- 命令行启动入口。
- 本地运行目录初始化。
- Web 服务端口占用检查。
- 控制台输出 URL，例如 `http://127.0.0.1:xxxx`。
- 基础健康检查接口。

### 不做

- 不实现 DoIP。
- 不实现 UDS。
- 不实现真实 Web 页面功能。

### 测试

- 启动进程后控制台输出 URL。
- 浏览器访问健康检查返回 OK。
- 端口占用时给出明确错误。
- Ctrl+C 能优雅退出。

### 单独提交建议

`init runtime skeleton`

## Step 2：配置模型与默认配置

### 目标

先冻结内部配置模型，避免后续协议、UI、ODX 都各自定义一套数据结构。

### 范围

- 默认配置文件。
- JSON 读写。
- 基础字段：VIN、EID、GID、逻辑地址、DoIP 端口、TLS 端口、源地址白名单。
- DID/DTC/Routine/Session/SecurityAccess/Flash 的空结构或最小结构。
- 配置校验和错误报告。

### 不做

- 不做 ODX 导入。
- 不做复杂版本迁移。
- 不做 Web 编辑器。

### 测试

- 缺失配置时生成默认配置。
- 合法配置可加载。
- 非法 VIN、非法端口、非法逻辑地址能报错。
- 保存后再次加载结果一致。

### 单独提交建议

`add config model`

## Step 3：Web 控制台空壳

### 目标

尽早形成“命令行启动后打开浏览器”的产品形态。

### 范围

- Vue 控制台基础页面。
- 服务状态卡片。
- 当前配置只读展示。
- 后端健康状态展示。
- 前后端基础 API 打通。

### 不做

- 不做复杂样式。
- 不做实时日志。
- 不做运行中修改配置。

### 测试

- 启动服务后打开 URL 能看到控制台。
- 控制台能显示当前服务状态。
- 控制台能显示当前 VIN、逻辑地址、端口。
- 刷新页面状态仍正确。

### 单独提交建议

`add web console shell`

## Step 4：事件与日志管道

### 目标

先建立跨模块事件总线，后续协议和 UI 都复用同一条日志路径，避免各模块自己写日志。

### 范围

- 结构化事件模型。
- 事件分类：系统、配置、连接、DoIP、UDS、状态机、异常注入。
- 文件日志异步写入。
- Web 控制台实时日志流。
- 日志等级和基础过滤。

### 不做

- 不做高吞吐优化。
- 不做复杂查询。
- 不做 pcap。

### 测试

- 启动事件写入文件。
- 配置加载事件写入文件。
- Web 控制台能实时看到事件。
- 快速产生大量测试事件时服务不崩溃。

### 单独提交建议

`add event logging pipeline`

## Step 5：DoIP UDP 发现闭环

### 目标

让真实诊断上位机能发现模拟 ECU，这是第一个外部可见协议闭环。

### 范围

- UDP 监听。
- 车辆公告 Vehicle Announcement。
- 车辆识别请求/响应。
- VIN、EID、GID、逻辑地址来自配置。
- DoIP 帧级日志。

### 不做

- 不做 TCP 诊断连接。
- 不做路由激活。
- 不做 TLS。

### 测试

- 用内部诊断上位机能发现 ECU。
- 用本地测试工具发送车辆识别请求，能收到正确响应。
- Web 控制台能看到 UDP 请求和响应。
- 错误 payload type 能记录为协议错误。

### 单独提交建议

`add doip udp discovery`

## Step 6：DoIP TCP 路由激活闭环

### 目标

建立诊断通信链路，但暂时只做到路由激活和空诊断消息处理。

### 范围

- TCP 监听。
- DoIP header 解码。
- 路由激活请求/响应。
- Alive Check。
- 源地址白名单校验。
- 连接生命周期事件。

### 不做

- 不做 UDS 业务服务。
- 不做 TLS。
- 不做复杂异常注入。

### 测试

- 诊断上位机能建立 TCP 连接。
- 路由激活成功。
- 非白名单源地址路由激活失败。
- 断开连接后 UI 状态更新。
- 多连接基础场景可见。

### 单独提交建议

`add doip tcp routing activation`

## Step 7：UDS 最小服务集

### 目标

形成第一个完整诊断闭环：DoIP TCP -> 路由激活 -> UDS 请求 -> UDS 响应。

### 范围

- UDS 服务分发框架。
- `0x10` DiagnosticSessionControl。
- `0x3E` TesterPresent。
- `0x22` ReadDataByIdentifier。
- 基础 NRC：服务不支持、子功能不支持、条件不满足、请求长度错误。
- 最小 ECU 状态：默认会话、扩展会话、编程会话。

### 不做

- 不做 SecurityAccess。
- 不做 DTC。
- 不做刷写。
- 不做 ResponsePending。

### 测试

- 客户端请求 `0x10` 可切换会话。
- 客户端请求 `0x3E` 返回正响应。
- 客户端读取配置内 DID 返回正确值。
- 未配置 DID 返回正确 NRC。
- Web 控制台能看到 UDS 请求、响应和会话变化。

### 单独提交建议

`add minimal uds services`

## Step 8：Web 实时观测闭环

### 目标

让工具真正可用于调试，而不是只能靠文件日志。

### 范围

- 连接列表。
- DoIP 报文列表。
- UDS 报文列表。
- 当前会话、安全状态、TesterPresent 状态展示。
- 日志过滤：连接、DoIP、UDS、状态机。

### 不做

- 不做复杂图表。
- 不做长期历史查询。
- 不做多用户。

### 测试

- 客户端连接后 UI 立即显示连接。
- 请求 `0x10/0x22/0x3E` 后 UI 显示请求和响应。
- 会话切换后 UI 状态变化。
- 断开连接后 UI 显示离线。

### 单独提交建议

`add realtime diagnostics view`

## Step 9：DID/DTC/Routine 配置闭环

### 目标

让 Web 控制台具备第一批运行中调试价值。

### 范围

- Web 修改 DID 值并立即生效。
- 注入/清除 DTC。
- 配置 Routine 的固定响应。
- 实现 `0x19`、`0x14`、`0x31` 的基础路径。
- 配置保存为 JSON。

### 不做

- 不做完整 ODX。
- 不做复杂 Routine 脚本。
- 不做持久化历史版本比较。

### 测试

- Web 修改 DID 后，客户端 `0x22` 读取到新值。
- Web 注入 DTC 后，客户端 `0x19` 读取到 DTC。
- 客户端 `0x14` 清除 DTC 后 UI 状态同步变化。
- 客户端 `0x31` 调用配置内 Routine 返回预期响应。

### 单独提交建议

`add configurable uds data`

## Step 10：核心状态机与 NRC

### 目标

提高 UDS 行为真实性，支持会话、安全、超时、前置条件和 ResponsePending。

### 范围

- TesterPresent 超时回退。
- SecurityAccess 状态结构。
- `0x27` 内置示例算法。
- P2/P2* 定时配置。
- `0x78 ResponsePending` 策略。
- 服务前置条件：会话限制、安全限制、下载流程限制。
- Web 改变会话和安全状态。

### 不做

- 不做 DLL 插件。
- 不做 TLS。
- 不做完整刷写数据校验。

### 测试

- 会话超时后状态回退。
- 未解锁调用受保护 DID/Routine 返回 NRC。
- 解锁后同一请求返回正响应。
- 配置 ResponsePending 后客户端先收到 `0x78`，再收到最终响应。
- Web 手动改变会话/安全状态后客户端行为变化。

### 单独提交建议

`add ecu state machine rules`

## Step 11：刷写主流程

### 目标

跑通诊断上位机常见刷写流程主路径，用于客户端流程验证。

### 范围

- `0x34` RequestDownload。
- `0x35` RequestUpload，若 MVP 需要可作为简化路径。
- `0x36` TransferData。
- `0x37` RequestTransferExit。
- 块序号校验。
- 下载状态迁移。
- 可配置总大小、块大小、允许会话和安全前置条件。

### 不做

- 不做真实刷写文件校验。
- 不做 OEM 完整签名验签。
- 不做复杂内存地址映射。

### 测试

- 正常 `0x34 -> 0x36*N -> 0x37` 成功。
- 未进入编程会话时请求下载返回 NRC。
- 未解锁时请求下载返回 NRC。
- 块序号错误返回 NRC。
- 中途断开连接后刷写状态可恢复到安全状态。

### 单独提交建议

`add flash transfer happy path`

## Step 12：PCAP 录制

### 目标

提供可交付的问题定位证据，支持 Wireshark 打开。

### 范围

- pcap 文件创建、写入、停止。
- 文件大小上限 500M。
- 录制状态在 Web 控制台显示。
- 文件轮转或停止策略。
- DoIP/TCP 数据包可被 Wireshark 识别。

### 不做

- 不保证 TLS 内容解密。
- 不做复杂报文索引。
- 不替代结构化日志。

### 测试

- 开启录制后执行一次 DoIP/UDS 请求。
- Wireshark 可打开 pcap。
- pcap 中能看到 TCP/DoIP 流量。
- 达到大小限制后按策略停止或轮转。

### 单独提交建议

`add pcap recording`

## Step 13：TLS 双向认证

### 目标

支持 DoIP over TLS 主路径和典型证书错误。

### 范围

- TLS 监听。
- 服务端证书配置。
- 客户端证书校验。
- 双向认证。
- 证书错误模拟：过期、不匹配、不受信任、缺失客户端证书。
- TLS 连接事件进入日志和 UI。

### 不做

- 不做完整证书管理系统。
- 不默认承诺 Wireshark 解密 TLS 内容。
- 不做 `0x84` 完整插件化。

### 测试

- 合法客户端证书可建立 TLS 连接。
- 非法证书连接失败且错误原因可见。
- TLS 下能完成路由激活和 `0x10/0x22/0x3E`。
- pcap 中能识别 TLS 流量。

### 单独提交建议

`add doip tls transport`

## Step 14：DLL 安全算法插件

### 目标

建立 OEM 安全算法扩展边界。

### 范围

- 定义 DLL ABI。
- 示例 DLL。
- 插件加载、卸载、版本检查。
- `0x27` seed/key 调用插件。
- `0x84` 预留插件入口或最小调用链。
- 调用超时和错误码。

### 不做

- 不接入不受控第三方插件市场。
- 不保证插件崩溃完全不影响进程，除非后续改为进程隔离。
- 不实现企业真实私有算法。

### 测试

- 示例 DLL 能成功加载。
- `0x27` 请求 seed 返回插件生成的 seed。
- 正确 key 解锁成功。
- 错误 key 返回 NRC。
- DLL 缺失、版本不匹配、调用超时均有明确错误。

### 单独提交建议

`add security dll plugin`

## Step 15：异常注入第一批

### 目标

先覆盖最高价值、最容易验收的异常场景，避免一次性跨所有协议层。

### 范围

- 响应延迟。
- 暂停/恢复响应。
- TCP 主动断开。
- 路由激活失败。
- 错误 inverse version。
- 错误 payload length。
- 手动 NRC。
- 自定义 UDS 响应。

### 不做

- 暂不做复杂乱序。
- 暂不做 TLS 深层故障全部组合。
- 暂不做概率型复杂策略编排。

### 测试

- Web 开启响应延迟后客户端感知延迟。
- Web 暂停响应后客户端超时。
- Web 触发断连后客户端连接断开。
- 路由激活失败可复现。
- 手动 NRC 能覆盖下一次指定服务响应。

### 单独提交建议

`add first fault injection set`

## Step 16：ODX/PDX 导入第一批

### 目标

先打通导入到内部配置的路径，不追求全量 ODX 兼容。

### 范围

- `.odx` 文件上传。
- `.pdx` 解压和入口文件识别。
- ECU 基本信息导入。
- DID 基础信息导入。
- 导入报告。
- 导入结果可保存为 JSON 配置。

### 不做

- 不做全量 ODX 解析。
- 不做复杂 Flash 配置完整映射。
- 不做所有工具链兼容认证。

### 测试

- 导入一个样例 `.odx` 后生成 DID 配置。
- 导入一个样例 `.pdx` 后生成 ECU 基本信息。
- 不支持字段进入跳过列表。
- 错误文件生成明确导入失败报告。

### 单独提交建议

`add odx pdx import subset`

## Step 17：MVP 压测与稳定性收敛

### 目标

把已经完成的闭环稳定到 MVP 指标。

### 范围

- 20 并发 TCP/TLS 连接。
- 200 请求/秒。
- 1 天稳定运行。
- 日志吞吐量 100M 目标验证。
- pcap 500M 上限验证。
- 资源指标展示：连接数、RPS、队列长度、内存、文件写入速率。
- 修复压测暴露的问题。

### 不做

- 不新增大功能。
- 不做大规模架构重写。
- 不扩大 ODX 范围。

### 测试

- 20 并发连接维持稳定。
- 200 RPS 下请求响应正确率达标。
- 连续运行 1 天无明显内存泄漏、句柄泄漏。
- 日志和 pcap 同时开启时 UI 不阻塞核心协议处理。

### 单独提交建议

`stabilize mvp performance`

## 推荐开发节奏

### 第一阶段：可启动、可访问、可观测

包含 Step 1 到 Step 4。

完成后应具备产品外壳：命令行启动、Web 控制台访问、配置加载、日志可见。

### 第二阶段：DoIP/UDS 最小闭环

包含 Step 5 到 Step 8。

完成后应具备最小诊断闭环：发现 ECU、路由激活、UDS 最小服务、Web 实时观测。

### 第三阶段：诊断真实性

包含 Step 9 到 Step 11。

完成后应具备可用于上位机主流程开发的 ECU 行为：DID/DTC/Routine、会话安全、NRC、刷写主流程。

### 第四阶段：安全、证据和异常

包含 Step 12 到 Step 15。

完成后应具备问题定位和异常验证能力：pcap、TLS、DLL 安全算法、第一批异常注入。

### 第五阶段：导入和稳定性

包含 Step 16 到 Step 17。

完成后应具备 MVP 交付能力：ODX/PDX 第一批导入、压测指标、长稳验证。

## 避免大重构的约束

- 配置模型从 Step 2 开始维护为唯一数据源，ODX、Web、UDS 都转换到这一模型。
- DoIP 网络传输只处理连接和字节流，不直接写 UDS 业务逻辑。
- DoIP 协议只负责 ISO 13400，不直接处理 DID/DTC/Routine。
- UDS 服务通过 ECU 状态机查询前置条件，不在每个服务里各自维护会话状态。
- Web 控制台只通过控制面 API 操作仿真内核，不直接触碰协议对象。
- 异常注入按层接入，先实现少量确定性场景，再扩展概率和组合策略。
- ODX/PDX 导入只输出标准配置，不直接修改运行中的协议状态。
- pcap 和业务日志分开实现，避免互相阻塞。

## 每步提交前检查清单

- 服务能启动。
- Web 控制台能打开。
- 默认配置能加载。
- 新增功能有最小自动化测试或手工验证步骤。
- 文件日志没有明显错误。
- 新增模块没有绕过既有模块边界。
- 本步提交只包含一个主题，不混入无关重构。
- 文档或配置示例随功能同步更新。
