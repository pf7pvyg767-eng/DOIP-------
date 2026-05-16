# Implementation Tasks: DID 配置和 `0x22` ReadDataByIdentifier

**Change ID:** `task-013`
**Status:** Apply 已完成
**Completed:** 2026-05-17

---

## Phase 1: DID 配置模型

- [x] 1.1 扩展 `DidConfig`，支持 16-bit DID ID、名称、固定字节值编码和值。
- [x] 1.2 保持现有配置加载/保存兼容，并为 DID 配置支持 `id`/`identifier` 最小兼容。
- [x] 1.3 扩展默认配置和 sample config，提供可读取的固定字节 DID 示例 `0xF190`。
- [x] 1.4 增加 DID ID 和固定十六进制字节值的配置校验。

**Quality Gate:**
- [x] 有效 DID 配置可以加载并通过校验。
- [x] 无效 DID ID 或无效 hex value 返回字段明确的校验错误。

---

## Phase 2: UDS `0x22` 服务

- [x] 2.1 新增 ReadDataByIdentifier 服务并注册到现有 UDS dispatcher。
- [x] 2.2 按每 2 字节解析请求 DID；空 payload 或奇数长度 payload 返回 `0x13`。
- [x] 2.3 单 DID 请求成功时返回 `0x62 DID value`。
- [x] 2.4 多 DID 请求成功时按请求顺序拼接 `DID value` 片段。
- [x] 2.5 任一 DID 未配置时返回 `0x31 RequestOutOfRange`，不返回部分成功数据。

**Quality Gate:**
- [x] `22 F1 90` 返回 `62 F1 90 ...`。
- [x] 多 DID 响应顺序由请求顺序决定。
- [x] 未配置 DID 和格式错误 NRC 均有单元测试覆盖。

---

## Phase 3: 运行时事件与 Host 接入

- [x] 3.1 通过 Host/DI 将配置中的 DID 数据提供给 `0x22` 服务。
- [x] 3.2 DID 成功读取时发布结构化 runtime event。
- [x] 3.3 DID 读取事件数据包含 DID ID 和该 DID 的响应长度。
- [x] 3.4 保持 DoIP 层只负责 Routing Activation 后 diagnostic forwarding，不在 DoIP 层实现 DID 业务逻辑。

**Quality Gate:**
- [x] DID 读取事件可通过现有 runtime event publisher 观察。
- [x] DoIP Routing Activation 后的 TCP 集成测试可读取 DID。

---

## Phase 4: 测试与验收

- [x] 4.1 增加单 DID 正响应单元测试。
- [x] 4.2 增加多 DID 顺序正响应单元测试。
- [x] 4.3 增加未配置 DID 返回 `0x31 RequestOutOfRange` 单元测试。
- [x] 4.4 增加奇数长度请求返回 `0x13 IncorrectMessageLengthOrInvalidFormat` 单元测试。
- [x] 4.5 增加 DID 读取事件包含 DID ID 和响应长度的测试。
- [x] 4.6 增加 Routing Activation 后 `0x22` DID 读取集成测试。
- [x] 4.7 执行 scope check，确认未实现动态表达式 DID、写 DID / `0x2E`、ODX/PDX 导入、UI、DTC、Routine、Flash 或 SecurityAccess 扩展。

**Quality Gate:**
- [x] `openspec validate task-013 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过，仅有非阻塞 `NU1900` NuGet vulnerability feed 访问警告。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过，89 passed, 0 failed, 0 skipped，仅有非阻塞 `NU1900` 警告。
- [x] 前端未变更，未运行 npm。
- [x] 未运行 `dotnet format`；该项仅作为带明确超时的非阻塞可选检查。

---

## Completion Checklist

- [x] DID 配置模型已扩展且保持固定字节值范围。
- [x] UDS `0x22` ReadDataByIdentifier 已实现。
- [x] 单 DID、多 DID、未配置 DID、奇数长度请求和事件验收均通过。
- [x] 未实现 out of scope 项。
- [x] OpenSpec、build、test 验证结果已记录。
- [x] 准备进入独立 Test & Status。
