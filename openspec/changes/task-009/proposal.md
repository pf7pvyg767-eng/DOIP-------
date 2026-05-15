# Proposal: UDP 车辆发现和公告

**Change ID:** `task-009`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

当前项目已经具备配置模型、Host 启动、结构化事件、Web 日志视图和 DoIP frame codec，但诊断客户端还无法通过 DoIP UDP discovery 发现模拟 ECU。后续诊断工具接入需要模拟器随 Host 启动监听 DoIP UDP 端口，处理 Vehicle Identification Request，并按配置发送 Vehicle Announcement。

本 change 需要补齐 UDP 车辆发现最小闭环：UDP 监听、车辆识别请求/响应、公告发送、响应内容从 `SimulatorConfig.entity` 获取，以及 DoIP 帧事件进入现有日志和 UI。范围不得扩展到 TCP、routing activation 或 TLS。

## Proposed Solution

- 新增 UDP DoIP server 或等价 transport 组件，随 Host 启动和停止生命周期启动/释放 UDP 监听。
- 使用 `SimulatorConfig.network.doipUdpPort` 和相关 bind 配置确定 UDP 监听端点。
- 新增 Vehicle Identification handler 或等价协议处理组件，基于 task-008 的 DoIP codec 解析 UDP datagram。
- 支持 Vehicle Identification Request，并返回 Vehicle Identification Response 或等价车辆识别响应 payload。
- 支持 Vehicle Identification Request with EID / VIN 的匹配响应；不匹配时不得返回错误业务流程，具体行为以 DoIP discovery 最小实现为准并通过测试固定。
- 从 `SimulatorConfig.entity` 读取 VIN、EID、GID、logical address 等响应字段。
- 按配置发送 Vehicle Announcement；配置项缺失时使用现有默认配置或最小默认策略，不引入新配置迁移。
- 将 UDP request、response 和 announcement 摘要发布为 `doip` category 的 `RuntimeEvent`，复用既有文件日志和 Web UI 事件流能力。
- 增加协议层单元测试和本地 UDP 集成测试，验证客户端可发送 discovery request 并收到响应。

## Scope

### In Scope

- UDP 监听 DoIP 端口。
- UDP 服务随 Host 启动并随 Host 停止释放。
- 处理 DoIP Vehicle Identification Request。
- 生成 Vehicle Identification Response。
- 支持 Vehicle Announcement 按配置定时广播或发送。
- 响应和公告内容来自 `SimulatorConfig.entity`。
- 复用 task-008 DoIP frame codec 处理 UDP datagram 的 frame 编解码。
- DoIP UDP 请求、响应和公告摘要写入现有结构化日志。
- DoIP UDP 请求、响应和公告摘要可通过现有 Web 日志 UI 看到。
- 增加单元测试、集成测试和 scope check。

### Out of Scope

- 不实现 TCP DoIP 监听或 TCP 连接管理。
- 不实现 routing activation。
- 不实现 TLS。
- 不实现 UDS 服务或诊断消息转发。
- 不实现 Alive Check、entity status、power mode 等其他 DoIP 业务。
- 不实现 PCAP 捕获、外部 observability 集成或日志搜索能力。
- 不新增 Web UI 页面或复杂图表；仅复用既有日志视图显示事件。
- 不改变 task-008 codec 的既定职责边界，除非为车辆发现 payload 增加必要的协议类型。

## Open Questions

- 原始 task 未给出 Vehicle Announcement 的完整配置字段名和默认间隔。实现应优先复用现有 `SimulatorConfig` 可表达的配置；如需要新增最小配置字段，应保持向后兼容并在默认配置中给出明确默认值。
- 原始 task 未明确 Vehicle Identification Request with EID / VIN 不匹配时是否静默忽略。实现应采用 DoIP discovery 常见的静默不响应策略，或在实现说明中明确选择；不得引入 routing activation 或错误会话流程。
- 原始 task 未明确公告发送目标地址。实现应采用配置指定目标或 UDP broadcast 的最小策略，并通过测试覆盖；若平台环境不支持广播测试，集成测试可使用本地可控端点。
- 原始 task 的“手工测试：内部诊断上位机能发现 ECU”依赖外部工具，自动验收应以本地 UDP client 集成测试和事件日志验证为主。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Maybe | 可能需要补充 announcement 最小配置项或复用现有配置；必须保持默认配置可用。 |
| Protocols.Doip | Yes | 增加车辆识别 request/response/announcement payload 编解码或 handler。 |
| Transport | Yes | 增加 UDP DoIP server、datagram 输入输出模型和生命周期释放。 |
| Host | Yes | 将 UDP 服务接入 Host 启停流程和配置加载。 |
| WebApi | No | 不新增 API；复用已有事件流和 recent events API。 |
| WebConsole | No | 不新增 UI；现有日志视图显示 `doip` 事件。 |
| Tests | Yes | 增加协议单元测试、UDP 集成测试、日志事件测试和 scope check。 |

## Architecture Considerations

- UDP transport 应与 DoIP 协议处理解耦：transport 负责 datagram 收发，handler 负责 codec 和 payload 处理。
- 协议处理应复用 task-008 的 DoIP frame codec，避免重复解析 header 或绕开错误模型。
- Vehicle Identification Response 和 Vehicle Announcement 的身份字段必须由 `SimulatorConfig.entity` 投影，避免在 transport 或 handler 中硬编码 VIN、EID、GID 或 logical address。
- UDP server 应通过 `CancellationToken`、`IHostedService` 或现有 runtime orchestration 模式管理生命周期，确保 Host 停止时释放端口。
- 事件发布应复用 task-006/task-007 的 `RuntimeEvent` 管道；本 change 只发布 `doip` 事件，不新增日志 API 或日志 UI。
- 集成测试应使用本地 UDP client 和临时端口或隔离配置，避免依赖固定系统端口导致测试不稳定。
- Announcement 定时任务应可取消，测试中应能用短间隔或手动触发机制验证，不应让测试依赖长时间等待。

## Acceptance Criteria

- [ ] UDP 服务随 Host 启动。
- [ ] Host 停止后 UDP 端口被释放。
- [ ] 收到 Vehicle Identification Request 后返回车辆识别响应。
- [ ] Vehicle Identification Response 的 VIN、EID、GID 和 logical address 来自 `SimulatorConfig.entity`。
- [ ] Vehicle Announcement 可按配置发送。
- [ ] Web 日志能看到请求和响应摘要。
- [ ] DoIP request、response 和 announcement 事件写入结构化日志，category 为 `doip`。
- [ ] 本地 UDP client 集成测试可以发送 discovery request 并收到响应。
- [ ] Scope check 确认未实现 TCP、routing activation 或 TLS。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| UDP 端口占用导致 Host 启动或测试不稳定 | Medium | Medium | 支持配置端口，测试使用隔离临时端口，并断言停止后释放资源。 |
| announcement 定时发送导致测试耗时或竞态 | Medium | Medium | 设计可取消、可配置间隔的发送机制，测试使用短间隔或可控触发。 |
| discovery handler 扩展到 routing activation 或 UDS | Medium | High | 在 scope/spec/tasks 中明确只处理 UDP vehicle identification 和 announcement。 |
| 响应字段被硬编码而不是来自配置 | Medium | High | 规格要求从 `SimulatorConfig.entity` 投影，单元测试使用非默认配置验证。 |
| Web UI 被误扩展 | Low | Medium | 只发布现有 `RuntimeEvent`，通过已有日志视图显示，不新增页面或复杂图表。 |
