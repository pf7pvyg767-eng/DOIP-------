# Proposal: DID 配置和 `0x22` ReadDataByIdentifier

**Change ID:** `task-013`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

`task-012` 已提供最小 ECU runtime state、DiagnosticSessionControl (`0x10`) 和 TesterPresent (`0x3E`)。诊断客户端接下来需要读取配置中的静态 DID 数据，尤其是常见的 VIN DID `0xF190`。当前 DID 配置仍是预留结构，UDS dispatcher 也尚未提供 `0x22` ReadDataByIdentifier 服务。

本 change 需要在不引入动态表达式、写 DID、ODX/PDX 导入或后续 UI 的前提下，扩展 DID 配置模型，支持从配置读取固定字节值 DID，并通过 UDS `0x22` 按请求顺序返回一个或多个 DID 的正响应。

## Proposed Solution

- 扩展现有 `SimulatorConfig.Uds.Dids` 数据模型，表达 DID ID、名称、固定字节值编码和值，以及只读访问所需的最小元数据。
- DID ID SHALL 支持 `0xF190` 这类 16-bit 十六进制标识。
- 固定字节 DID SHALL 支持十六进制字节串配置，并在读取时编码为原始字节。
- 新增 `0x22` ReadDataByIdentifier UDS 服务，注册到现有 UDS dispatcher。
- 请求 payload SHALL 按每 2 字节解析一个 DID；请求长度为奇数或没有 DID 时返回 `0x13 IncorrectMessageLengthOrInvalidFormat`。
- 当全部请求 DID 均已配置且可读取时，服务 SHALL 返回 `0x62 DID value ...`，并按请求中的 DID 顺序拼接多个 DID 的响应片段。
- 任一请求 DID 未配置时，服务 SHALL 返回 NRC `0x31 RequestOutOfRange`，不得返回部分成功数据。
- DID 读取成功时 SHALL 写入现有运行时事件管道，事件包含 DID ID 和该 DID 的响应长度。

## Scope

### In Scope

- 扩展 DID 配置模型以支持固定字节值 DID。
- 校验 DID ID 与固定十六进制字节值的基础格式。
- 实现 UDS `0x22` ReadDataByIdentifier。
- 支持单个请求读取多个 DID。
- 多 DID 正响应按请求顺序返回。
- 未配置 DID 返回 NRC `0x31 RequestOutOfRange`。
- 请求长度为奇数或不包含完整 DID 时返回 NRC `0x13 IncorrectMessageLengthOrInvalidFormat`。
- DID 读取事件包含 DID ID 和响应长度。
- 为单 DID、多 DID、未配置 DID、奇数长度请求和事件内容增加测试。

### Out of Scope

- 不支持动态表达式 DID。
- 不支持写 DID / `0x2E` WriteDataByIdentifier。
- 不支持 ODX/PDX 导入。
- 不实现 DID 的复杂会话权限矩阵或 SecurityAccess 解锁逻辑。
- 不实现后续 UI、Web API 管理、DTC、Routine、Flash、SecurityAccess 扩展。
- 不改变 DoIP Routing Activation、TCP/TLS、PCAP 或 UDP discovery 行为。

## Open Questions

- 原始 task 示例使用 `id` 字段，而当前代码已有 `DidConfig.Identifier` 预留字段。实现应优先保持与现有模型兼容；如需要支持 `id` 别名，应限定为 DID 配置反序列化兼容，不扩大为通用 schema migration。
- 原始 task 未指定 DID 读取事件的事件名和完整字段集合。实现应复用现有 runtime event 结构，至少包含 DID ID 和响应长度。
- 原始 task 未指定当多 DID 请求中某个 DID 未配置时是否允许部分成功。为保持 UDS 负响应语义，本 proposal 规定任一 DID 未配置时整体返回 `0x31`。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 扩展 DID 配置模型，支持 DID ID、固定字节值编码和值。 |
| Configuration Validation | Yes | 增加 DID ID 和固定十六进制字节值格式校验。 |
| Protocols.Uds | Yes | 新增 `0x22` ReadDataByIdentifier 服务并注册到 dispatcher。 |
| Core Runtime Events | Yes | 发布 DID 读取事件，包含 DID ID 和响应长度。 |
| Protocols.Doip | Maybe | 复用既有 diagnostic forwarding；只需通过集成测试验证 Routing Activation 后可读取 DID。 |
| Host | Yes | 注入配置中的 DID 数据并注册 `0x22` 服务。 |
| WebApi | No | 不新增 API。 |
| WebConsole | No | 不新增 UI。 |
| Tests | Yes | 增加 UDS service、配置模型、事件和 DoIP TCP 集成测试。 |

## Architecture Considerations

- `0x22` 应作为 `IUdsService` 注册到现有 UDS dispatcher，DoIP 层继续只负责 diagnostic payload 转发和响应封装。
- DID 配置应保持数据契约性质，只表达固定字节值，不执行脚本、表达式或运行时计算。
- DID 查找应以 16-bit DID ID 为键，但响应必须保留请求顺序，避免 map 枚举顺序影响协议输出。
- 未配置 DID 应通过现有 negative response 模型返回 `RequestOutOfRange (0x31)`。
- DID 读取事件应复用现有 runtime event publisher，不新增单独日志系统。
- 本 change 不应引入 SecurityAccess、session 权限 enforcement 或 DID 写入服务；这些可由后续 task 独立定义。

## Acceptance Criteria

- [x] `22 F1 90` 返回以 `62 F1 90 ...` 开头的正响应。
- [x] 一个 `0x22` 请求包含多个 DID 时，响应按请求 DID 顺序返回。
- [x] 请求未配置 DID 时返回 NRC `0x31 RequestOutOfRange`。
- [x] 请求长度为奇数时返回 NRC `0x13 IncorrectMessageLengthOrInvalidFormat`。
- [x] DID 读取事件包含 DID ID 和响应长度。
- [x] Scope check 确认未实现动态表达式 DID、写 DID / `0x2E`、ODX/PDX 导入、UI、DTC、Routine、Flash 或 SecurityAccess 扩展。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| DID 配置被扩展成动态表达式执行 | Medium | High | spec 明确只支持固定十六进制字节值，测试和 scope check 覆盖。 |
| 多 DID 响应顺序受字典枚举影响 | Medium | Medium | 规格要求按请求顺序逐个解析和拼接，测试覆盖顺序。 |
| 未配置 DID 返回部分成功数据 | Medium | Medium | 规格规定任一未配置 DID 整体返回 `0x31`。 |
| DID 事件字段与现有事件模型不一致 | Low | Medium | 只要求 DID ID 和响应长度，事件结构复用现有 runtime event data。 |
| 实现顺手加入 `0x2E` 或权限体系 | Medium | High | tasks 和 acceptance 明确列入 out of scope，并要求 scope check。 |
