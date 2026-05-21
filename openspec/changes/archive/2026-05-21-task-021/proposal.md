# Proposal: PCAP 录制 MVP

**Change ID:** `task-021`
**Created:** 2026-05-19
**Status:** Implementation Complete
**Completed:** 2026-05-19

---

## Problem Statement

当前模拟器已经具备 UDP DoIP discovery、TCP Routing Activation、UDS 主路径和运行时事件能力，但缺少可落盘的网络抓包文件。用户在定位 DoIP/UDS 联调问题时，需要把一次 UDP 发现和 TCP UDS 交互保存为 Wireshark 可打开的 `.pcap` 文件，并能在 Web 控制台看到录制状态。

本 change 只覆盖 task-021 要求的 PCAP 录制 MVP：生成标准 pcap 文件、记录 TCP/UDP DoIP 收发数据、提供开始/停止/状态查询、执行 500MiB 文件大小上限控制，并在 Web 显示录制状态。

## Proposed Solution

- 在 Observability 层新增最小 pcap writer，写入 pcap global header 与 packet record header，并以可被 Wireshark 识别的链路类型保存 UDP/TCP DoIP 收发数据。
- 新增 pcap recorder 运行时服务，统一接收 UDP/TCP DoIP 收发方向、时间戳、端点和 payload，负责录制状态、文件命名、字节统计、上限检查和停止。
- 在 WebApi 提供 `GET /api/pcap/status`、`POST /api/pcap/start`、`POST /api/pcap/stop`，返回 task 指定的数据契约。
- WebConsole 增加或扩展 PCAP 状态展示，显示是否正在录制、文件路径、已写入字节数和 500MiB 上限。
- 达到 500MiB 上限时采用“停止录制并发布事件”的最小策略，不在本 task 中实现轮转策略。

## Scope

### In Scope

- 实现标准 `.pcap` writer，包含 global header 和 packet header 写入。
- 记录 UDP DoIP discovery 收发数据。
- 记录 TCP DoIP/UDS 通道收发数据。
- 支持开始录制、停止录制、状态查询。
- 状态契约包含 `recording`、`filePath`、`bytesWritten`、`maxBytes`。
- 默认文件大小上限为 `524288000` bytes，也就是 500MiB。
- 达到大小上限时停止录制，并发布可观测运行时事件。
- Web 控制台显示当前 PCAP 录制状态。
- 单元测试覆盖 pcap header 与大小上限行为。
- 集成测试覆盖一次 UDP/TCP 交互录制后文件非空。

### Out of Scope

- 不保证 TLS 内容解密。
- 不做 pcapng 高级元数据。
- 不做报文索引搜索。
- 不实现 pcap 下载、报文回放或图表分析。
- 不扩大到 TLS 传输、ODX/PDX、SecurityAccess 插件、异常注入或诊断业务新能力。
- 不改变 DoIP/UDS 协议处理语义；PCAP 只观察和记录已发生的网络收发数据。
- 不实现长期性能调优、后台上传、压缩归档或复杂保留策略。

## Open Questions

- 原 task 允许“停止或轮转”。本 proposal 采用“达到上限后停止并产生事件”的最小策略；若后续需要轮转，应由独立 task 明确文件命名、保留数量和 UI 行为。
- 原 task 未指定 pcap 链路类型与 IP/TCP/UDP 头构造策略。实现应选择 Wireshark 可直接打开并能识别 UDP/TCP 流量的最小格式，并用测试验证 global header、packet header 和生成文件可读性。
- 原 task 未指定 start 请求是否允许传入自定义文件名或目录。MVP 应优先使用默认 `logs/pcap/session-<timestamp>.pcap` 命名，除非现有配置模型已有明确输出目录约定。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Observability | Yes | 新增 pcap writer 和 recorder，负责文件写入、状态、大小上限和事件。 |
| UDP DoIP | Yes | 在收发路径旁路记录 UDP discovery 数据，不改变 discovery 响应语义。 |
| TCP DoIP | Yes | 在 TCP 收发路径旁路记录 DoIP/UDS 数据，不改变 Routing Activation 或 UDS 转发语义。 |
| Runtime Events | Yes | 复用现有事件模型发布 `pcap` 分类的开始、停止、上限到达或错误事件。 |
| WebApi | Yes | 新增 `GET /api/pcap/status`、`POST /api/pcap/start`、`POST /api/pcap/stop`。 |
| WebConsole | Yes | 展示 PCAP 当前录制状态、文件路径、已写入字节数和上限。 |
| Tests | Yes | 新增 writer 单元测试、recorder 上限测试、UDP/TCP 录制集成测试。 |

## Architecture Considerations

- PCAP 录制属于 Observability 能力，应作为旁路观察组件接入 UDP/TCP 收发路径，不应把文件写入逻辑放入协议业务处理器。
- Recorder 应串行化文件写入，避免并发 TCP/UDP 收发同时写入导致 pcap record 交错或 header 损坏。
- Start/stop/status API 应调用同一个 recorder 状态源，避免 Web UI 与实际写入状态不一致。
- 大小上限检查应在写入 packet record 前执行，避免生成超过上限的文件；上限到达后 recorder 应停止接收新记录并发布事件。
- Writer/recorder 错误应通过运行时事件和 API 状态可观测，不应阻塞或改变核心 DoIP/UDS 协议处理。

## Acceptance Criteria

- [x] 开启录制后生成 `.pcap` 文件。
- [x] 执行 UDP discovery 和 TCP UDS 请求后，生成的 `.pcap` 文件非空。
- [x] 生成文件可被 Wireshark 打开。
- [x] 达到 500MiB 大小上限后停止录制，并产生运行时事件。
- [x] `GET /api/pcap/status` 返回当前录制状态、文件路径、已写入字节数和上限。
- [x] `POST /api/pcap/start` 可开始录制，`POST /api/pcap/stop` 可停止录制。
- [x] Web 控制台能显示录制状态和字节计数。
- [x] Scope check 确认未实现 TLS 解密、pcapng 高级元数据、报文索引搜索、ODX/PDX、SecurityAccess 插件或异常注入。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| pcap 文件格式不被 Wireshark 识别 | Medium | High | 单元测试验证 global header 和 packet header，集成/手工验收用 Wireshark 打开生成文件。 |
| 录制写入阻塞协议收发 | Medium | High | 将录制设计为旁路观察能力，写入错误不改变 DoIP/UDS 主流程，并限制 MVP 不做复杂分析。 |
| 文件超过 500MiB 上限 | Medium | Medium | 写入每个 packet 前检查预计大小，达到上限时停止录制并发布事件。 |
| TCP/UDP 记录点遗漏方向或端点 | Medium | Medium | Recorder 输入契约包含方向、transport、端点和 payload，集成测试覆盖 UDP 与 TCP。 |
| scope 膨胀到 TLS/pcapng/搜索 | Low | High | proposal、tasks 和 spec 明确排除 TLS 解密、pcapng 元数据和索引搜索。 |
