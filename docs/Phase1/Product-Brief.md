# Product Brief：Windows DoIP ECU 模拟器

## 1. 产品目标

构建一个运行在 Windows 平台上的本地 DoIP ECU 模拟器，用于诊断上位机开发工程师在没有真实 ECU 的情况下，完成 DoIP/UDS 诊断链路开发、调试、压测和异常场景验证。

产品以命令行方式启动服务，启动后输出本地 Web 控制台 URL，用户通过浏览器进入控制台进行实时观测、配置调整和异常注入。

模拟器目标覆盖：

- ISO 13400 DoIP 核心通信能力
- UDS on DoIP，基于 ISO 14229
- DoIP over TLS 真实握手
- 双向证书认证
- 证书配置与证书错误模拟
- SecuredDataTransmission `0x84`
- OEM 安全算法 DLL 插件化
- 实时网络日志、文件日志、Wireshark 可识别 pcap 输出

系统只模拟一个独立 DoIP Entity，并只支持一个逻辑地址。

## 2. 目标用户

主要目标用户是企业内部的诊断上位机开发工程师。

用户需要使用该工具验证内部诊断上位机是否能够：

- 发现 DoIP ECU
- 完成车辆识别
- 建立 TCP/TLS 连接
- 完成路由激活
- 执行 UDS 诊断服务
- 处理 ECU 状态机、NRC、超时、异常网络行为
- 在压测和异常场景下保持正确行为

## 3. 核心使用场景

### 3.1 DoIP 连接与发现调试

模拟器支持车辆公告 UDP、车辆识别请求/响应、实体状态、诊断电源模式、路由激活、Alive Check、TCP_DATA/TLS_DATA。

可配置项包括端口、VIN、EID、GID、逻辑地址、源地址白名单等。

### 3.2 UDS 服务完整模拟

支持常用核心 UDS 服务：

- `0x10` 诊断会话控制
- `0x11` ECU Reset
- `0x14` ClearDiagnosticInformation
- `0x19` ReadDTCInformation
- `0x22` ReadDataByIdentifier
- `0x27` SecurityAccess
- `0x28` CommunicationControl
- `0x2E` WriteDataByIdentifier
- `0x2F` InputOutputControlByIdentifier
- `0x31` RoutineControl
- `0x34` RequestDownload
- `0x35` RequestUpload
- `0x36` TransferData
- `0x37` RequestTransferExit
- `0x3E` TesterPresent
- `0x85` ControlDTCSetting
- `0x84` SecuredDataTransmission

ECU 配置需要包含一定数量的 DID、DTC、Routine、SecurityAccess、Session、刷写下载流程配置。

### 3.3 真实 UDS 状态机模拟

需要模拟：

- 会话状态
- TesterPresent 超时
- SecurityAccess 解锁状态
- NRC 条件
- `0x78 ResponsePending`
- P2/P2* 定时
- 服务间前置条件
- 刷写流程状态迁移

### 3.4 TLS 与安全诊断验证

支持 DoIP over TLS 真实握手、证书配置、双向认证、证书错误模拟。

SecurityAccess `0x27` 和 SecuredDataTransmission `0x84` 的安全算法通过 DLL 插件扩展。

### 3.5 实时调试型 Web 控制台

控制台偏向实时观测和调试，同时具备一定配置管理能力。

运行中支持：

- 修改 DID 值
- 注入 DTC
- 改变会话状态
- 改变安全解锁状态
- 切换网络异常策略
- 暂停/恢复响应
- 手动发送 NRC
- 手动发送自定义响应
- 查看实时 DoIP/UDS/连接/状态机日志

### 3.6 异常网络行为模拟

需要可复现以下异常：

- 丢包
- 延迟
- 乱序
- TCP 断开
- 半包/粘包
- 错误 payload type
- 错误 inverse version
- 错误 length
- 路由激活失败
- TLS 握手失败
- 证书过期/不匹配

### 3.7 配置导入与保存

支持 Web 控制台编辑配置，并保存为 JSON/YAML。

ODX 导入第一阶段需要支持：

- `.odx`
- `.pdx`
- ECU 基本信息
- DID
- DTC
- Routine
- SecurityAccess
- Session
- Flash 下载相关配置

### 3.8 日志与 pcap 输出

日志粒度包括：

- DoIP 帧级
- UDS 服务级
- TCP/TLS 连接事件
- 状态机变化
- 配置变更
- 异常注入事件

pcap 文件必须能被 Wireshark 直接识别，并支持按 DoIP/TLS 解码分析。

## 4. 非目标范围

第一阶段不包含：

- 多个独立 DoIP Entity
- 多个逻辑 ECU 地址
- 模拟车辆网关承载多个 ECU
- 对外公开 SDK
- REST/WebSocket/CLI 自动化接口
- Python SDK
- CI 无界面运行模式
- 兼容 Vector、Softing、ETAS 等第三方工具的专项认证
- Linux/macOS 支持
- Docker/WSL 部署形态
- 云端部署
- 多用户权限系统
- 企业级账号、审计、远程协作
- 真实 ECU 刷写数据校验的完整 OEM 级实现，除非通过配置或 DLL 插件补充

## 5. MVP 成功标准

### 5.1 启动与访问

在 Windows 命令行启动服务后，控制台输出一个本地 URL，用户复制到浏览器后可进入 Web 控制台。

### 5.2 DoIP 基础链路

内部诊断上位机能够发现模拟 ECU，完成车辆识别、实体状态查询、诊断电源模式查询、TCP/TLS 连接、路由激活和 Alive Check。

### 5.3 UDS 核心服务

能跑通核心 UDS 服务，至少覆盖诊断会话、TesterPresent、DID 读写、DTC 查询/清除、SecurityAccess、RoutineControl、刷写下载流程主路径。

### 5.4 状态机真实性

会话、安全解锁、TesterPresent 超时、P2/P2*、NRC、`0x78 ResponsePending`、刷写流程状态迁移可配置且行为可复现。

### 5.5 TLS 与安全

支持 DoIP over TLS 双向认证，可配置证书，并能模拟典型证书错误。SecurityAccess 至少支持 DLL 插件调用。

### 5.6 Web 实时调试

控制台可以实时查看连接、DoIP/UDS 报文、状态变化和日志；支持运行中修改 DID、注入 DTC、切换异常策略、手动 NRC/自定义响应。

### 5.7 异常注入

可复现关键网络和协议异常，包括延迟、丢包、断连、错误 DoIP header、路由激活失败、TLS 握手失败。

### 5.8 日志与 pcap

支持界面实时日志、文件日志和 pcap 输出。pcap 文件大小上限 500M，并能被 Wireshark 直接打开和识别。

### 5.9 性能指标

MVP 目标性能：

- 并发 TCP/TLS 连接数：20
- 每秒诊断请求数：200
- 稳定运行时长：1 天
- 日志吞吐量：100M
- pcap 文件大小上限：500M

### 5.10 配置管理

支持 Web 控制台编辑并保存 JSON/YAML 配置；支持导入 `.odx`/`.pdx` 中的 ECU 基本信息和主要诊断配置。

## 6. 主要风险

### 6.1 “完整支持”范围较大

ISO 13400、ISO 14229、TLS、安全诊断、刷写流程、ODX/PDX 解析同时覆盖，范围很大。需要拆分阶段，否则 MVP 容易失控。

### 6.2 ODX/PDX 复杂度高

ODX 数据结构复杂，不同企业或工具链生成的 ODX 差异明显。第一阶段应定义明确支持子集，避免承诺通用 ODX 全量解析。

### 6.3 TLS pcap 可解析性存在限制

TLS 流量天然加密。Wireshark 可以识别 TLS，但若要解密诊断内容，可能需要导出 key log、私钥或额外配置。需要提前定义“识别”和“解密分析”的验收边界。

### 6.4 异常注入与协议正确性容易冲突

半包、粘包、错误 length、乱序、TLS 失败等异常需要精确控制网络层行为。实现时要避免异常注入破坏正常协议栈稳定性。

### 6.5 状态机复杂度高

UDS 服务之间存在会话、安全、时序、前置条件和刷写阶段依赖。如果状态机设计不清晰，后续扩展 DID、Routine、DTC、下载流程会变得难维护。

### 6.6 DLL 插件安全与稳定性风险

DLL 插件可能崩溃、阻塞、内存泄漏或带来安全风险。需要设计调用超时、错误隔离、ABI 版本管理和示例插件。

### 6.7 高吞吐日志与实时 UI 压力

100M 日志吞吐量、实时界面显示、文件落盘、pcap 输出同时存在时，容易造成 UI 卡顿或 IO 瓶颈。需要日志分层、采样、环形缓冲和异步写入。

### 6.8 单 Entity 单逻辑地址会限制后续扩展

当前范围明确只支持一个 DoIP Entity 和一个逻辑地址。若未来要扩展到网关或多 ECU，需要提前在架构上保留实体模型和地址模型的扩展空间。
