# Implementation Tasks: Flash 下载主流程 `0x34-0x37`
**Change ID:** `task-020`

---

## Phase 1: Flash 配置模型

- [x] 1.1 扩展或补齐 Flash 配置模型，支持 `enabled`、`maxMemorySize`、`maxBlockLength`、`allowedSessions`、`securityRequired`。
- [x] 1.2 为 Flash 配置增加字段级验证：大小必须为正、块长度必须为正且不超过总大小、允许会话必须有效。
- [x] 1.3 将默认配置设置为保守可测试值，避免未配置时产生不受限下载能力。
- [x] 1.4 确认配置仅表达协议模拟能力，不包含文件路径、签名策略、真实地址段表或 OEM 策略。

**Quality Gate:**
- [x] 默认配置可加载并通过验证。
- [x] 无效 Flash 配置返回清晰字段级错误。
- [x] 未引入文件写入、签名、地址映射或策略配置。

---

## Phase 2: Flash 下载运行时状态

- [x] 2.1 在 ECU runtime state 或等价组件中增加内存内下载状态。
- [x] 2.2 状态至少维护活动标记、总大小、已接收大小、最大块长度、期望块序号、请求参数和完成状态。
- [x] 2.3 定义状态初始化、推进、完成、取消/清理方法。
- [x] 2.4 定义重复 `0x34`、无活动下载时 `0x36/0x37`、完成后继续传输等状态异常处理。
- [x] 2.5 增加单元测试覆盖状态初始化、推进、完成、错误不推进和清理。

**Quality Gate:**
- [x] 下载状态只保存在内存中。
- [x] 错误请求不会错误推进状态。
- [x] 状态行为可由单元测试稳定验证。

---

## Phase 3: `0x34` RequestDownload 主路径

- [x] 3.1 注册并实现 `0x34` UDS service。
- [x] 3.2 校验 Flash 是否启用、当前会话是否为允许的编程会话。
- [x] 3.3 当配置要求安全访问时，校验所需 SecurityAccess 状态已解锁。
- [x] 3.4 解析最小支持的 requestDownload 参数：dataFormatIdentifier、addressAndLengthFormatIdentifier、memoryAddress、memorySize。
- [x] 3.5 拒绝不支持格式、无效大小、超过 `maxMemorySize` 或已有活动下载的请求，并返回明确 NRC。
- [x] 3.6 成功时初始化下载状态，并返回 `0x74` 正响应和可接受的最大块长度。
- [x] 3.7 增加单元测试覆盖成功初始化、未编程会话、未解锁、格式错误、大小超限。

**Quality Gate:**
- [x] `0x34` 成功后下载状态正确初始化。
- [x] 未进入编程会话返回明确 NRC。
- [x] 未解锁返回明确 NRC。
- [x] 不支持或超限请求不会初始化下载状态。

---

## Phase 4: `0x36` TransferData 主路径

- [x] 4.1 注册并实现 `0x36` UDS service。
- [x] 4.2 校验当前存在活动下载状态。
- [x] 4.3 校验块序号等于期望块序号，错误时返回明确 NRC 且不推进状态。
- [x] 4.4 校验本次数据长度不超过配置/协商块大小，累计接收不超过总大小。
- [x] 4.5 成功时累计已接收大小、递增期望块序号并返回 `0x76` 正响应。
- [x] 4.6 覆盖多个 `0x36` 分块直到累计大小达到 `0x34` 声明总大小。
- [x] 4.7 增加单元测试覆盖正常多块、块序号错误、无活动下载、块长度超限、累计大小超限。

**Quality Gate:**
- [x] `0x36` 正常推进下载状态。
- [x] 块序号错误返回明确 NRC 且状态不推进。
- [x] 大小边界被稳定验证。

---

## Phase 5: `0x37` RequestTransferExit 主路径

- [x] 5.1 注册并实现 `0x37` UDS service。
- [x] 5.2 校验当前存在活动下载状态。
- [x] 5.3 校验已接收大小满足 `0x34` 声明的总大小。
- [x] 5.4 成功时返回 `0x77` 正响应，并将下载状态标记完成或清理。
- [x] 5.5 若传输未完整、无活动下载或状态不一致，返回明确 NRC。
- [x] 5.6 增加单元测试覆盖正常结束、未完整结束、无活动下载和完成后状态。

**Quality Gate:**
- [x] `0x37` 成功完成 `0x34 -> 0x36*N -> 0x37` 主路径。
- [x] 未完整传输不能被错误标记为完成。
- [x] 完成后的状态收敛明确。

---

## Phase 6: 断连处理与 DoIP TCP 集成

- [x] 6.1 在 TCP/诊断连接关闭路径触发 Flash 下载状态清理，或进入明确可恢复安全状态。
- [x] 6.2 断连处理应发布运行时事件或日志，便于验证状态已清理或可恢复。
- [x] 6.3 增加集成测试：经 DoIP TCP 完成 Routing Activation 后执行 `0x34 -> 0x36*N -> 0x37`。
- [x] 6.4 增加集成或组件测试：中途断连后下载状态被清理或进入可恢复安全状态。
- [x] 6.5 确认 DoIP 层只转发 UDS payload，不直接实现 Flash 业务。

**Quality Gate:**
- [x] DoIP TCP 完整刷写主路径通过。
- [x] 中途断连行为可验证。
- [x] DoIP 层未承载 Flash 业务逻辑。

---

## Phase 7: 验证与范围检查

- [x] 7.1 运行 `openspec validate task-020 --strict`。
- [x] 7.2 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 7.3 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 7.4 执行 acceptance check：正常下载、会话异常、安全异常、块序号异常、断连处理、DoIP TCP 集成。
- [x] 7.5 执行 scope check：确认未做真实文件写入、签名验签、完整内存映射、ECU reset 联动、ODX/PDX、PCAP、TLS、SecurityAccess DLL 插件、Web 编辑、复杂调度器或 OEM 策略。

**Quality Gate:**
- [x] OpenSpec strict validation 通过。
- [x] 构建和测试通过。
- [x] 验收标准全部通过。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] Flash 配置模型完成。
- [x] 下载运行时状态完成。
- [x] `0x34` RequestDownload 主路径完成。
- [x] `0x36` TransferData 主路径完成。
- [x] `0x37` RequestTransferExit 主路径完成。
- [x] 会话 gating 完成。
- [x] 安全 gating 完成。
- [x] 块序号错误处理完成。
- [x] 中途断连状态清理或可恢复安全状态完成。
- [x] 单元测试覆盖正常流程、会话异常、安全异常、块序号异常。
- [x] DoIP TCP 集成测试覆盖完整刷写主路径。
- [x] 未实现任何 out_of_scope 项。
- [x] 准备进入 Apply。
