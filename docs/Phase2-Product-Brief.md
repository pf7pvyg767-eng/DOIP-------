# 阶段 2 产品简报：MVP 产品化与端到端验收链

## 1. 阶段定位

阶段 2 不是继续扩大功能面的阶段，而是把阶段 1 已经实现或铺垫的能力产品化。

本阶段的核心目标是：以 Standard ECU 端到端主流程为第一验收链，把 DoIP Simulator 从“功能已经有形状”打磨成“真实可用、流程顺畅、可自动验收、证据可复查”的本地 DoIP/UDS 测试仪验证工具。

阶段 2 不追求一次性覆盖完整 OEM ECU 平台，也不接受只停留在 UI 展示、mock 数据、占位实现或手动演示的功能。每个进入阶段 2 的能力，都必须能进入真实工作流，并通过自动化验收证明它可用。

阶段 2 的产品定位是：

> 一个面向测试仪开发者的本地 DoIP/UDS 验证目标，提供真实 Standard ECU 主流程、常用 UDS 服务集、运行时观测、PCAP 证据和轻量 UI 操作闭环。

它不应被定位为：

> 一个完整的 OEM ECU 工程平台，或一个只展示界面但后端未真实打通的演示工具。

## 2. 目标用户

主要用户是正在构建或改进 DoIP 上层诊断测试仪应用的诊断测试仪开发者。

用户需要通过模拟器回答以下问题：

- 我的测试仪能否正确发现 DoIP ECU？
- 它能否完成 Routing Activation 并保持 TCP 诊断连接？
- 它能否正确发送和解析常用 UDS 服务？
- 它能否在 Web Console、日志和 PCAP 中留下可复查证据？
- 当测试仪行为异常时，我能否快速定位是连接、协议、配置、服务逻辑还是抓包证据出了问题？

## 3. 第一验收链：Standard ECU 最小真实闭环

阶段 2 的第一条主流程固定为 Standard ECU 最小真实闭环。它是后续所有扩展能力的产品化基准。

### 3.1 主流程

第一验收链应覆盖：

1. 本地启动模拟器。
2. 打开 Web Console。
3. Web Console 显示真实运行状态，而不是静态或 mock 状态。
4. 用户选择 Standard ECU 场景。
5. 测试仪完成 UDP Vehicle Identification。
6. 测试仪完成 TCP Routing Activation。
7. 测试仪发送常用 UDS 服务请求。
8. 模拟器返回真实协议响应。
9. Web Console 显示活跃连接、最近 DoIP/UDS 报文、运行时事件和当前场景。
10. 用户开始和停止 PCAP。
11. 模拟器生成可用于 Wireshark 分析的 PCAP 文件。
12. 用户从 UI 执行 shutdown。
13. 模拟器停止监听、关闭事件流、刷新日志并释放端口。

### 3.2 第一批 UDS 服务集

第一批主流程必须覆盖测试仪常用基础服务集：

- `0x10` DiagnosticSessionControl。
- `0x3E` TesterPresent。
- `0x22` ReadDataByIdentifier。
- `0x2E` WriteDataByIdentifier。
- `0x27` SecurityAccess。
- `0x19` ReadDTCInformation。
- `0x14` ClearDiagnosticInformation。
- `0x31` RoutineControl。

这些服务不能只返回固定 mock 响应。它们必须接入真实配置、运行时状态、事件日志和必要的协议校验。

### 3.3 第一验收链的完成标准

Standard ECU 主流程只有同时满足以下条件，才算完成：

- 测试仪可以真实连接模拟器并完成 DoIP discovery 与 routing activation。
- 常用 UDS 服务集可以通过真实协议交互得到预期响应。
- Web Console 展示的数据来自真实 runtime 状态。
- 每个关键动作都产生运行时事件。
- PCAP 可以开始、停止并生成实际文件。
- 自动化验收脚本可以验证主流程结果。
- 轻量 Web Console 冒烟测试可以验证关键 UI 操作入口。
- UI shutdown 可以真实停止运行时并释放端口。

## 4. 完成定义

阶段 2 的每个功能都必须满足“真实可用”的完成定义。

### 4.1 不接受的完成状态

以下状态不能算完成：

- UI 已展示，但后端仍使用 mock 数据。
- 后端逻辑存在，但 Web Console 没有真实操作入口或真实状态展示。
- 只有手动演示，没有自动化验收。
- 只验证 happy path，没有事件、日志或 PCAP 证据。
- 只在单个开发者环境可用，没有明确启动、验证和复查命令。
- 任务完成说明中没有列出实际运行过的验证命令。

### 4.2 必须满足的完成状态

每个阶段 2 change 必须至少包含：

- 明确的真实行为定义。
- 后端真实实现。
- Web Console 真实数据接入，若该能力有 UI 表达。
- 运行时事件。
- 日志或 PCAP 等可复查证据。
- CLI 自动化验收。
- 必要的轻量 Web Console 冒烟测试。
- 明确的回归命令和结果记录。

## 5. AI 开发工作流

阶段 2 的工作流要适合 AI 反复开发、验证和归档。每个 change 都应小而完整，围绕一段真实端到端能力闭环组织。

### 5.1 每个 Change 的标准流程

每个 change 应按以下顺序推进：

1. 写清 OpenSpec proposal、delta spec 和 tasks。
2. 明确禁止 mock、占位和只展示 UI 的实现方式。
3. 先定义验收脚本或验收命令。
4. 实现后端协议、runtime 状态和配置接入。
5. 接入 Web Console 的真实数据和操作入口。
6. 添加运行时事件和必要日志。
7. 添加或更新 PCAP/证据输出。
8. 运行 CLI 自动化验收。
9. 运行轻量 Web Console 冒烟测试。
10. 记录验证命令、输出结论和剩余风险。

### 5.2 日常验收方式

阶段 2 日常任务采用：

> CLI 主验收 + 少量 Web Console 冒烟。

CLI 主验收负责：

- 启动或连接本地模拟器。
- 执行 DoIP/UDS 协议交互。
- 验证响应字节、状态变化和 NRC。
- 检查运行时事件。
- 检查日志或 PCAP 文件是否生成。
- 返回可被 AI 和 CI 使用的明确 pass/fail 结果。

Web Console 冒烟负责：

- 页面能打开。
- 场景选择可用。
- 运行状态显示真实数据。
- 抓包开始/停止入口可用。
- 关键事件或最近报文能显示。
- shutdown 入口存在并需要确认。

### 5.3 安装验证策略

MSI 安装验证不进入每个日常开发任务。

安装验证作为阶段末统一发布门禁，在 P0 主链路完成后执行。发布门禁应验证：

- MSI 可以安装。
- 可以从开始菜单启动模拟器。
- Web Console 可以打开。
- Standard ECU 主流程冒烟通过。
- UI shutdown 可以释放端口。
- 卸载或重装不会破坏基础使用路径。

这样可以保持日常 AI 开发反馈循环足够快，同时保留最终交付质量门槛。

## 6. 产品范围

### 6.1 P0：Standard ECU 主链路产品化

P0 只聚焦第一条真实可用闭环：

- 本地启动和 runtime 状态可见。
- Standard ECU 场景选择。
- UDP Vehicle Identification。
- TCP Routing Activation。
- 常用 UDS 服务集：`0x10`、`0x3E`、`0x22`、`0x2E`、`0x27`、`0x19`、`0x14`、`0x31`。
- 真实事件流。
- Web Console 关键状态展示。
- PCAP 开始/停止和文件生成。
- UI shutdown。
- CLI 主验收脚本。
- Web Console 冒烟测试。

P0 的目标是让用户能用一个真实 Standard ECU 场景完成测试仪基础联调。

### 6.2 P1：主链路扩展与健壮性

P1 在 P0 闭环基础上扩展：

- TLS DoIP 和 TLS key log。
- 更多故障注入场景。
- 场景 profile 切换。
- 兼容性测试用例 runner。
- 场景命名 PCAP。
- 更完整的 Wireshark 指导。
- 更完整的结果记录和测试报告草稿。

P1 的目标是把“能跑通标准流程”扩展为“能调试常见测试仪问题”。

### 6.3 P2：高级仿真能力

P2 只在 P0/P1 稳定后考虑：

- 半包/粘包仿真。
- 增强 DTC 模型。
- 增强 Routine 动作。
- 面向测试仪相关 DID 数据的 ODX/PDX 导入改进。
- 可导出的测试报告。
- 更复杂的故障组合。

P2 的目标是增强测试覆盖，而不是修补基础工作流。

## 7. 关键功能要求

### 7.1 Runtime Control

Web Console 应提供受控 shutdown 动作。

shutdown 应：

- 要求用户确认。
- 停止 DoIP UDP/TCP/TLS 监听器。
- 停止当前 PCAP 录制。
- 刷新运行时日志。
- 关闭 WebSocket 事件流。
- 退出模拟器进程。
- 释放端口。

UI 应显示：

- Web API 端口。
- DoIP UDP/TCP 端口。
- TLS 端口，若启用。
- 配置路径。
- 日志路径。
- PCAP 目录。
- 当前进程 ID。
- 当前场景。

### 7.2 Standard ECU 场景

Standard ECU 场景必须是可执行配置，而不是 UI 标签。

它应定义：

- VIN DID 和至少一组固定 DID。
- 可写 DID。
- 基础 DTC 数据。
- SecurityAccess seed/key 行为。
- RoutineControl 固定响应。
- 会话和 TesterPresent 行为。
- Routing Activation 接受条件。
- 源地址白名单。

### 7.3 DoIP 主链路

DoIP 主链路必须支持：

- UDP Vehicle Identification。
- TCP Routing Activation。
- Alive Check，若当前协议层已支持。
- DoIP Diagnostic Message。
- 源地址校验。
- 逻辑地址校验。
- 连接生命周期事件。

### 7.4 UDS 常用服务集

UDS 服务必须接入真实 runtime 状态：

- `0x10` 应改变或确认当前诊断会话。
- `0x3E` 应刷新 TesterPresent 状态。
- `0x22` 应读取当前 DID runtime 值。
- `0x2E` 应更新可写 DID runtime 值。
- `0x27` 应使用配置的 SecurityAccess 行为。
- `0x19` 应返回基础 DTC 数据。
- `0x14` 应清除或更新 DTC 状态。
- `0x31` 应返回配置的 RoutineControl 响应。

每个服务都必须对无效输入返回明确 NRC，而不是静默失败。

### 7.5 PCAP 和证据

PCAP 工作流应支持：

- 开始录制。
- 停止录制。
- 显示当前文件路径。
- 显示已写入字节数。
- 生成可被 Wireshark 打开的文件。
- 在 CLI 验收中检查文件存在和非空。

阶段 2 P0 不要求每次 PCAP 都达到完整 TCP 栈级精度，但必须对测试仪调试有实际帮助，并且不能是空文件或假路径。

### 7.6 Web Console

Web Console 应服务于主链路，而不是堆砌面板。

P0 必须清晰展示：

- 当前 runtime 状态。
- 当前场景。
- 活跃连接。
- 最近一帧 DoIP。
- 最近一次 UDS 请求。
- 最近一次 UDS 响应。
- 最近事件。
- PCAP 状态。
- shutdown 操作。

## 8. 自动化验收要求

### 8.1 CLI 主验收

P0 必须提供一条标准命令，用于验证 Standard ECU 主流程。

该命令应覆盖：

- 模拟器启动或连接。
- UDP discovery。
- Routing Activation。
- 常用 UDS 服务集。
- 事件检查。
- PCAP 检查。
- shutdown 或清理。

命令输出必须适合 AI 读取：

- 成功时明确列出通过项目。
- 失败时明确指出失败步骤、期望值和实际值。
- 返回非零退出码表示失败。

### 8.2 Web Console 冒烟

P0 必须提供轻量 Web Console 冒烟测试。

冒烟测试至少覆盖：

- 页面加载。
- runtime 状态显示。
- Standard ECU 场景可见或已选中。
- PCAP 控件可见。
- 事件区域可见。
- shutdown 控件可见并需要确认。

### 8.3 回归要求

任何修改 DoIP、UDS、runtime、事件、PCAP 或 Web Console 状态展示的 change，都必须运行相关 CLI 主验收或对应子集，并记录结果。

## 9. 成功标准

阶段 2 P0 成功的标准是：

- Standard ECU 主流程可以在本地开发环境稳定运行。
- 测试仪可以完成 DoIP discovery 和 routing activation。
- 常用 UDS 服务集真实可用。
- Web Console 显示真实 runtime 状态。
- PCAP 可以生成真实文件。
- UI shutdown 可以释放端口。
- CLI 主验收通过。
- Web Console 冒烟通过。
- 没有依赖 mock 数据才能通过的核心流程。

阶段 2 完整成功的标准是：

- P0 主链路稳定。
- P1 的 TLS、故障注入、场景 runner 等能力挂接到同一产品工作流。
- 阶段末 MSI 安装验证通过。
- 用户可以在没有源码的情况下完成基础测试仪联调。

## 10. 关键风险

### 10.1 继续堆功能但主流程不可用

如果阶段 2 继续按功能点扩展，而不围绕主流程验收，产品仍可能保持“很多能力都有一点，但用户无法顺畅使用”的状态。

缓解方式：所有 P0 change 都必须服务于 Standard ECU 第一验收链。

### 10.2 Mock 和假 UI 回流

AI 开发容易生成看起来完整但没有真实后端连接的 UI。

缓解方式：完成定义中明确禁止 mock；每个 UI 能力必须有真实数据来源和冒烟测试。

### 10.3 自动化验收过重

如果每个任务都要求完整安装、完整浏览器 E2E 和全量协议测试，反馈循环会变慢。

缓解方式：日常采用 CLI 主验收 + 少量 Web Console 冒烟；MSI 安装验证放到阶段末。

### 10.4 PCAP 可信度不足

如果 PCAP 不能用于基本 Wireshark 分析，测试仪调试价值会下降。

缓解方式：P0 至少保证文件真实生成、非空、路径可见、与主流程相关；更高精度 TCP/TLS 证据放入 P1/P2。

## 11. 阶段 2 工作原则

- 先闭环，再扩展。
- 先真实，再漂亮。
- 先自动验收，再声明完成。
- 每个 change 都应小而完整。
- 每个 UI 都必须接真实状态。
- 每个协议行为都必须有事件或证据。
- 安装包质量在阶段末统一验证，不拖慢日常开发循环。
