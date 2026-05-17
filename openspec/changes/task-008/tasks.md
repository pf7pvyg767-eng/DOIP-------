# Implementation Tasks: DoIP 帧编解码核心

**Change ID:** `task-008`

---

## Phase 1: Protocol Contracts

- [x] 1.1 新增 `DoipSimulator.Protocols.Doip` 项目、命名空间或等价协议层位置（如尚不存在）。
- [x] 1.2 定义 DoIP payload type 枚举或等价强类型封装，并覆盖常用已知 payload type。
- [x] 1.3 确保 payload type 可保留未知 `ushort` 原始值，并可被调用方识别为未知类型。
- [x] 1.4 定义 `DoipHeader` 或等价基础类型，包含 protocol version、inverse protocol version、payload type 和 payload length。
- [x] 1.5 定义 `DoipFrame` 或等价基础类型，包含 header 信息和 payload bytes。

**Quality Gate:**
- [x] 基础类型可被后续 payload parser 复用。
- [x] 未知 payload type 不会丢失原始值。

---

## Phase 2: Error And Result Model

- [x] 2.1 定义 codec result 类型，例如 `DecodeResult<T>` 或等价成功/失败返回模型。
- [x] 2.2 定义明确错误模型，例如 `DoipProtocolError`、错误码枚举或等价类型。
- [x] 2.3 错误模型区分 header 长度不足、protocol version 不支持、inverse version 不匹配、payload length 不一致和编码输入无效。
- [x] 2.4 错误结果包含可测试的错误码和足够定位问题的消息或字段信息。

**Quality Gate:**
- [x] 单元测试可直接断言错误类型/错误码。
- [x] codec 不用模糊异常承载正常协议错误。

---

## Phase 3: Header And Frame Codec

- [x] 3.1 实现 DoIP header 解码，固定读取 8 字节 header。
- [x] 3.2 实现 protocol version 校验。
- [x] 3.3 实现 inverse protocol version 校验，要求 inverse version 与 protocol version 互为按位取反。
- [x] 3.4 实现 payload length 校验，要求 header 中长度与实际 payload bytes 长度一致。
- [x] 3.5 实现完整 DoIP frame 解码，返回 header、payload type 原始值和 payload bytes。
- [x] 3.6 实现 DoIP frame 编码，输出 protocol version、inverse version、payload type、payload length 和 payload bytes。
- [x] 3.7 编码时处理 payload length 与 payload 实际长度不一致的输入，采用自动计算或明确错误策略，并保持行为可测试。
- [x] 3.8 确保所有多字节字段使用网络字节序。

**Quality Gate:**
- [x] 合法 frame 可 round-trip。
- [x] 不合法 header/frame 返回明确错误。
- [x] codec API 只依赖内存字节序列。

---

## Phase 4: Tests

- [x] 4.1 增加合法 DoIP header/frame encode/decode round-trip 单元测试。
- [x] 4.2 增加 inverse version 错误测试。
- [x] 4.3 增加 payload length 小于实际 payload 的错误测试。
- [x] 4.4 增加 payload length 大于实际 payload 的错误测试。
- [x] 4.5 增加 header 长度不足错误测试。
- [x] 4.6 增加不支持 protocol version 错误测试。
- [x] 4.7 增加未知 payload type 保留和上报测试。
- [x] 4.8 增加测试或代码结构断言，确认 codec 测试不启动网络服务、不绑定端口。

**Quality Gate:**
- [x] DoIP codec 测试均为纯单元测试。
- [x] 所有关键错误路径均有断言。

---

## Phase 5: Verification And Scope Check

- [x] 5.1 执行 `openspec validate task-008 --strict`。
- [x] 5.2 执行 `.NET` build。
- [x] 5.3 执行 `.NET` test。
- [x] 5.4 核对 acceptance criteria。
- [x] 5.5 执行 scope check，确认未实现 UDP/TCP socket、routing activation、UDS 服务、Web UI 或事件流扩展。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] 后端 build/test 通过。
- [x] 验收标准全部满足。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] DoIP payload type 枚举或等价类型已定义。
- [x] 未知 payload type 可保留并上报。
- [x] DoIP header 基础类型已定义。
- [x] DoIP frame 基础类型已定义。
- [x] DoIP header 编码已实现。
- [x] DoIP header 解码已实现。
- [x] protocol version 校验已实现。
- [x] inverse version 校验已实现。
- [x] payload length 校验已实现。
- [x] 明确错误模型已提供。
- [x] 后续 payload 解析可复用的基础类型已提供。
- [x] 合法 DoIP frame round-trip 测试已覆盖。
- [x] inverse version 错误测试已覆盖。
- [x] payload length 不一致错误测试已覆盖。
- [x] 未知 payload type 保留和上报测试已覆盖。
- [x] codec 测试不需要启动网络服务。
- [x] 未实现 UDP/TCP socket。
- [x] 未实现 routing activation 业务。
- [x] 未实现 UDS 服务。
- [x] 未扩展 Web UI 或事件流。
- [x] OpenSpec 严格校验已执行。
