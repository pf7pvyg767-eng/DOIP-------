# Implementation Tasks: P2/P2*、TesterPresent 超时和 ResponsePending
**Change ID:** `task-019`

---

## Phase 1: 配置模型与验证

- [x] 1.1 扩展或复用 Session 配置，支持每个诊断会话的 P2/P2* 参数。
- [x] 1.2 增加 TesterPresent 超时配置，包含启用状态和固定超时时长。
- [x] 1.3 增加服务级响应延迟配置，至少支持 `serviceId`、`responsePending.enabled`、`initialDelayMs`、`finalDelayMs`。
- [x] 1.4 补充配置验证：拒绝无效 serviceId、负延迟、无效 P2/P2*、无效 TesterPresent 超时。

**Quality Gate:**
- [x] 默认配置保持可加载并通过验证。
- [x] 无效定时配置返回清晰字段级错误。
- [x] 未配置响应延迟的服务保持原有同步响应行为。

---

## Phase 2: TesterPresent 超时回退

- [x] 2.1 在 ECU runtime state 或等价组件中维护 TesterPresent 最近时间、超时截止和当前定时状态摘要。
- [x] 2.2 实现 TesterPresent 超时检测，超时后将非默认会话回退到默认会话。
- [x] 2.3 超时回退时发布运行时事件或结构化日志，包含旧会话、新会话和超时原因。
- [x] 2.4 添加可测试的时钟或触发机制，避免测试依赖真实长等待。
- [x] 2.5 增加单元测试覆盖未超时不回退、超时后回退、默认会话下超时不产生错误切换。

**Quality Gate:**
- [x] TesterPresent 超时后会话回退默认会话。
- [x] 回退事件或日志可被测试观察。
- [x] 未引入复杂调度器或持久化定时状态。

---

## Phase 3: `0x10` P2/P2* 配置响应

- [x] 3.1 修改 DiagnosticSessionControl 服务，使 `0x10` 正响应使用配置的 P2/P2*。
- [x] 3.2 对默认会话、扩展会话和编程会话分别覆盖配置值编码。
- [x] 3.3 保持不支持子功能、格式错误和现有会话事件行为不回归。
- [x] 3.4 增加单元测试验证 `0x50 subFunction P2 P2*` 包含配置值。

**Quality Gate:**
- [x] `0x10` 响应包含配置的 P2/P2*。
- [x] 未配置时使用明确默认值并保持现有测试稳定。

---

## Phase 4: ResponsePending 与服务级延迟

- [x] 4.1 增加 UDS 响应调度或包装能力，按服务级配置处理固定延迟。
- [x] 4.2 配置 `responsePending.enabled = true` 时，先返回 `7F SID 78`。
- [x] 4.3 在最终延迟后返回原服务最终响应，并保持响应顺序稳定。
- [x] 4.4 配置仅最终延迟但不启用 ResponsePending 时，不发送 `0x78`。
- [x] 4.5 确保单个连接或请求的延迟不阻塞其他连接基础处理。
- [x] 4.6 增加单元测试覆盖 ResponsePending 响应序列和未启用时的行为。
- [x] 4.7 增加集成测试验证客户端收到 `0x78` 和最终响应。
- [x] 4.8 增加并发或多连接测试验证其他连接仍可完成基础请求。

**Quality Gate:**
- [x] 配置 ResponsePending 后先返回 `7F SID 78`，再返回最终响应。
- [x] 定时行为不阻塞其他连接的基础处理。
- [x] 未配置服务不受 ResponsePending 包装影响。

---

## Phase 5: Web/API 定时状态展示

- [x] 5.1 扩展 ECU 状态快照或等价 API，暴露定时状态只读摘要。
- [x] 5.2 Web 展示当前会话、最近 TesterPresent、TesterPresent 超时截止或服务延迟状态摘要。
- [x] 5.3 确保 Web 仅展示状态，不新增定时配置编辑 UI。
- [x] 5.4 增加后端/API 或前端测试，覆盖定时状态字段展示或映射。

**Quality Gate:**
- [x] Web/API 可以展示定时状态。
- [x] 未新增人工编辑延迟配置的 UI。

---

## Phase 6: 验证与范围检查

- [x] 6.1 运行 `openspec validate task-019 --strict`。
- [x] 6.2 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 6.3 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 6.4 如 Web 前端文件发生变更，运行对应前端 build。
- [x] 6.5 执行 scope check：确认未实现复杂调度器、概率型延迟、完整 OEM 时序策略、Flash/ISO-TP 多帧时序或 Web 编辑能力。

**Quality Gate:**
- [x] OpenSpec strict validation 通过。
- [x] 构建和测试通过。
- [x] 验收标准全部通过。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] TesterPresent 超时回退默认会话完成。
- [x] 超时回退日志或事件完成。
- [x] `0x10` 配置 P2/P2* 响应完成。
- [x] 服务级固定响应延迟完成。
- [x] ResponsePending 后最终响应序列完成。
- [x] Web 定时状态只读展示完成。
- [x] 非阻塞多连接行为验证完成。
- [x] 未实现任何 out_of_scope 项。
- [x] 准备进入 Apply。
