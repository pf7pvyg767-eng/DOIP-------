# Proposal: DoIP 帧编解码核心

**Change ID:** `task-008`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

当前项目已经具备配置模型、运行时启动、配置 API、基础控制台和运行事件能力，但还缺少独立、可测试的 DoIP 帧编解码核心。后续 DoIP payload 解析、routing activation、UDS 透传或诊断响应都需要先有稳定的基础帧契约和 header codec。

本 change 需要在不引入 socket、网络服务或业务协议流程的前提下，定义 DoIP payload type、DoIP frame/header 数据结构、编码/解码流程和明确错误模型，使 codec 可以通过纯单元测试验证。

## Proposed Solution

- 新增 DoIP payload type 枚举或等价类型，覆盖常用已知 payload type，并允许保留未知 payload type 原始值。
- 新增 `DoipFrame` 或等价基础类型，包含 protocol version、inverse protocol version、payload type、payload length 和 payload bytes。
- 新增 `DoipHeader` 或等价基础类型，用于后续 payload 解析复用固定 8 字节 header 信息。
- 新增 `IDoipCodec` / `DoipCodec` 或等价 codec API，支持从字节序列解码 DoIP frame，并将 frame 编码为字节序列。
- 解码时校验 header 最小长度、protocol version、inverse version 和 payload length 与实际 payload 长度的一致性。
- 编码时生成符合 DoIP header 格式的字节序列，并保持 payload type 原始值。
- 新增明确错误模型，例如错误码/错误类型可区分 header 过短、protocol version 不支持、inverse version 不匹配、payload length 不一致等情况。
- 增加纯单元测试覆盖合法 frame round-trip、inverse version 错误、payload length 错误、未知 payload type 保留与上报，以及 codec 不依赖网络服务。

## Scope

### In Scope

- 定义 DoIP payload type 枚举或等价强类型封装。
- 实现 DoIP header 编码和解码。
- 支持完整 DoIP frame 的基础编码和解码。
- 校验 protocol version。
- 校验 inverse protocol version 是否与 protocol version 互为按位取反。
- 校验 payload length 是否与实际 payload 长度一致。
- 提供明确、可测试、可断言的 codec 错误模型。
- 保留并上报未知 payload type，不因未知值导致 codec 崩溃。
- 提供后续 payload 解析可复用的 header/frame/result/error 基础类型。
- codec 必须独立可测试，不依赖 UDP/TCP socket、Host 启动或运行中的网络服务。
- 增加 `DoipSimulator.Protocols.Doip` 相关单元测试或等价测试覆盖。

### Out of Scope

- 不实现 UDP/TCP socket。
- 不启动或依赖运行中的网络服务。
- 不实现 routing activation 业务流程。
- 不实现 UDS 服务。
- 不解析具体 DoIP payload 业务体。
- 不实现诊断消息转发、车辆发现、Alive Check 或实体状态业务。
- 不扩展 Web UI。
- 不扩展事件流或控制台日志视图。
- 不引入 TLS、PCAP、持久化日志或外部 observability 集成。

## Open Questions

- 任务未指定支持的 DoIP protocol version 集合。实现应优先支持 ISO 13400 常用版本 `0x02`，如项目已有约定则以项目约定为准；不支持版本应返回明确错误。
- 任务未指定 `DecodeResult<T>` 的具体形态。实现可采用现有项目风格的 result 类型，或新增最小可用的成功/失败结果类型，但必须便于单元测试断言错误码和错误消息。
- 任务未指定未知 payload type 的 API 表达方式。实现应保留 `ushort` 原始值，并在类型或结果中提供可判断未知值的方式。
- 任务未指定编码时是否允许调用方传入与 payload 实际长度不一致的 `PayloadLength`。实现应选择一致且可测试的策略：要么编码时根据 payload 自动计算长度，要么对不一致输入返回/抛出明确错误；不得静默生成无效 frame。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | No | 现有配置和事件核心不需要修改。 |
| Protocols.Doip | Yes | 新增 DoIP frame、payload type、codec、错误模型和基础 result 类型。 |
| Host/WebApi | No | codec 不接入 socket、Host 或 WebApi 运行流程。 |
| WebConsole | No | 不扩展 UI 或事件流。 |
| Tests | Yes | 增加纯单元测试验证 codec 行为，不启动网络服务。 |

## Architecture Considerations

- DoIP codec 应位于协议层命名空间/项目，例如 `DoipSimulator.Protocols.Doip`，避免放入 Host、WebApi 或 UI。
- codec API 应只依赖内存字节序列，例如 `ReadOnlySpan<byte>`、`byte[]` 或等价结构；不得直接依赖 socket、stream listener、HTTP/WebSocket 或后台服务。
- DoIP header 固定为 8 字节：protocol version、inverse protocol version、payload type、payload length。payload type 使用 2 字节网络字节序，payload length 使用 4 字节网络字节序。
- 解码应先完成 header 长度检查，再校验 protocol version、inverse version 和 payload length，错误结果需要明确指出失败原因。
- 未知 payload type 是协议兼容性场景，不应导致解析崩溃；codec 应保留原始 `ushort` 并让调用方判断其是否为已知枚举值。
- 后续 payload 解析应能复用本 change 产生的 `DoipHeader`、`DoipFrame`、payload type 和错误模型，而不需要重新解析 header。
- 测试应只实例化 codec 并传入字节数组，避免使用端口、socket、WebApi host 或进程级服务。

## Acceptance Criteria

- [ ] 合法 DoIP frame 可以正确 encode/decode round-trip。
- [ ] inverse version 错误返回明确错误。
- [ ] payload length 与实际长度不一致返回明确错误。
- [ ] protocol version 不支持返回明确错误。
- [ ] header 长度不足返回明确错误。
- [ ] 未知 payload type 可被保留并上报，不导致 codec 崩溃。
- [ ] codec 测试不需要启动网络服务。
- [ ] Scope check 确认未实现 UDP/TCP socket、routing activation、UDS 服务、Web UI 或事件流扩展。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| codec 实现提前扩展到 routing activation 或 UDS payload 解析 | Medium | Medium | 在 scope 和 spec 中明确仅处理 header/frame 基础编解码，payload 业务解析留给后续 task。 |
| 未知 payload type 被错误视为致命错误 | Medium | Medium | 规格要求保留原始 `ushort` 并提供未知值上报能力，单元测试覆盖该场景。 |
| payload length 字节序或长度校验不一致 | Medium | High | 规格明确使用网络字节序，并要求 round-trip、过短/过长测试。 |
| codec 与 socket 或 Host 绑定导致测试复杂 | Low | High | 规格要求 codec 只依赖内存字节输入输出，测试不得启动网络服务。 |
| 错误模型过于模糊导致调用方无法定位失败原因 | Medium | Medium | 规格要求可断言的错误码/错误类型，测试覆盖每类关键错误。 |
