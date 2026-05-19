# Proposal: Flash 下载主流程 `0x34-0x37`
**Change ID:** `task-020`
**Created:** 2026-05-18
**Status:** Implementation Complete

---

## Problem Statement

当前模拟器已经具备基础 DoIP TCP 诊断转发、UDS dispatcher、会话控制、SecurityAccess 与响应时序能力，但尚未支持诊断刷写下载主路径。诊断上位机在开发和回归测试中需要通过 `0x34 RequestDownload`、多次 `0x36 TransferData`、`0x37 RequestTransferExit` 验证刷写流程编排、状态保持、块序号处理、会话与安全 gating。

本 change 只补齐 task-020 要求的 Flash 下载 MVP 主流程。它用于协议流程验证，不写真实文件、不做签名验签、不实现完整内存映射，也不引入复杂刷写调度器或 OEM 策略。

## Proposed Solution

- 扩展 Flash 配置模型，支持启用状态、最大内存大小、最大块长度、允许会话和是否需要 SecurityAccess。
- 在 ECU runtime state 或等价组件中维护一次下载会话的内存内状态，包括总大小、已接收大小、最大块长度、期望块序号、请求参数和完成状态。
- 实现 `0x34 RequestDownload` 主路径：校验编程会话、安全解锁、Flash 配置、请求格式和长度约束；成功后初始化下载状态并返回可接受的最大块长度。
- 实现 `0x36 TransferData` 主路径：校验已有下载状态、块序号、块长度和累计大小；成功后推进下载状态并递增期望块序号。
- 实现 `0x37 RequestTransferExit` 主路径：校验下载状态完整性；成功后结束下载并进入完成或清理状态。
- 处理 TCP/诊断连接中途断连：清理当前下载状态，或进入明确可恢复的安全状态；该行为必须可测试、可观察。
- 不纳入 `0x35 RequestUpload`，避免扩大主路径范围。

## Scope

### In Scope

- Flash 配置模型与字段级验证。
- `0x34 RequestDownload` 下载初始化主路径。
- `0x36 TransferData` 下载数据传输主路径。
- `0x37 RequestTransferExit` 下载结束主路径。
- 下载状态维护：总大小、已接收大小、块大小、期望块序号、完成/活动状态。
- 会话 gating：未进入编程会话时返回明确 NRC。
- 安全 gating：未完成所需 SecurityAccess 解锁时返回明确 NRC。
- 块序号错误时返回明确 NRC，并保持下载状态不被错误推进。
- 中途断连后的下载状态清理，或进入明确可恢复安全状态。
- 单元测试覆盖正常下载流程、会话异常、安全异常、块序号异常。
- 集成测试通过 DoIP TCP 完成 `0x34 -> 0x36*N -> 0x37` 主路径。

### Out of Scope

- 不实现 `0x35 RequestUpload`，除非后续独立 task 明确要求。
- 不做真实文件写入、文件系统持久化或二进制落盘。
- 不做签名验签、摘要校验或完整性校验算法。
- 不做完整内存地址映射、地址段权限表或 ECU 分区模型。
- 不做刷写后 ECU reset 联动。
- 不扩展 ODX/PDX、PCAP、TLS、SecurityAccess DLL 插件或 Web 编辑能力。
- 不引入复杂刷写调度器、并行刷写流程或 OEM 刷写策略。
- 不实现刷写压缩、加密传输、断点续传或跨进程持久化恢复。

## Open Questions

- 原始 task 未指定 `0x34` 的 addressAndLengthFormatIdentifier 支持矩阵。实现应选择最小、明确、可测试的格式集合，并对不支持格式返回现有项目约定的明确 NRC。
- 原始 task 未指定具体 NRC 数值。实现应沿用现有 UDS 服务的 NRC 约定，并在测试中验证语义清晰的拒绝原因。
- 原始 task 未指定断连后采用“清理状态”还是“可恢复安全状态”。实现应二选一并记录在测试和运行时事件中；不得扩展为持久化断点续传。
- 原始 task 未指定同一 ECU 是否允许并发下载。实现应采用单 ECU 单活动下载状态；并发请求应返回明确 NRC 或拒绝结果。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 扩展 Flash 配置模型和验证，包含启用状态、最大大小、最大块长度、允许会话和安全要求。 |
| Core Runtime State | Yes | 增加内存内 Flash 下载状态，维护总大小、已接收大小、块长度、块序号和活动/完成状态。 |
| Protocols.Uds | Yes | 注册并实现 `0x34`、`0x36`、`0x37` 主路径服务。 |
| Security/Session State | Yes | 读取既有会话和 SecurityAccess 状态执行 gating，不改变 SecurityAccess 算法语义。 |
| DoIP TCP | Maybe | 需要确保 TCP 诊断路径可转发并返回完整刷写主路径响应，断连时触发状态清理或安全恢复。 |
| Web/API | No | 不新增 Web 编辑或展示需求，除非现有状态快照自然暴露运行时事件。 |
| Tests | Yes | 增加 UDS 单元测试和 DoIP TCP 集成测试覆盖主路径与异常路径。 |

## Architecture Considerations

- Flash 下载状态应位于 ECU runtime state 或等价核心状态组件，UDS service 只推进协议状态，不直接写文件。
- `0x34`、`0x36`、`0x37` 应复用现有 UDS dispatcher、NRC 构造、会话状态和 SecurityAccess 状态，不在 DoIP 层实现业务逻辑。
- 状态只应保存在内存中，并随连接断开、transfer exit 成功或错误恢复策略而清理/收敛。
- 块序号应按 UDS 常见 1 字节 sequence counter 递增并处理回绕；若项目已有约定，应优先复用。
- Flash 配置默认值必须保守，避免未配置时意外开启大范围下载能力。

## Acceptance Criteria

- [x] `0x34 -> 0x36*N -> 0x37` 正常完成。
- [x] 未进入编程会话时返回明确 NRC。
- [x] 未解锁所需安全等级时返回明确 NRC。
- [x] 块序号错误时返回明确 NRC，且下载状态不被错误推进。
- [x] 中途断连后下载状态被清理，或进入可恢复安全状态。
- [x] 单元测试覆盖正常下载流程、会话异常、安全异常、块序号异常。
- [x] 集成测试通过 DoIP TCP 跑完整刷写主路径。
- [x] Scope check 确认未做真实文件写入、签名验签、完整内存映射、ECU reset 联动、Web 编辑、复杂调度器或 OEM 策略。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Flash 状态机范围膨胀 | Medium | High | 仅实现 `0x34/0x36/0x37` MVP 主路径，tasks/spec 明确排除调度器、策略和持久化。 |
| NRC 语义与现有服务不一致 | Medium | Medium | 复用现有 NRC 构造和命名约定，测试验证异常语义。 |
| 断连清理与连接生命周期耦合不清 | Medium | High | 在 DoIP/TCP 连接关闭路径增加明确状态清理或安全恢复钩子，并加入集成测试。 |
| 块序号、大小边界处理出错 | Medium | High | 单元测试覆盖序号错误、长度超限、累计大小边界和完成后继续传输。 |
| 误加入真实刷写副作用 | Low | High | Scope check 禁止文件写入、签名、地址映射、reset 和 Web 编辑能力。 |
