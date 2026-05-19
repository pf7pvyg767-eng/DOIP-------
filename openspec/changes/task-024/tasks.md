# Implementation Tasks: 异常注入第一批

**Change ID:** `task-024`

## Phase 1: Fault Profile 配置和运行时状态

- [x] 1.1 新增 `FaultProfile` 配置模型，覆盖 `enabled`、`responseDelayMs`、`pauseResponses`、`routingActivationFailure`、`corruptNextDoipHeader`、下一次 NRC 和自定义 UDS 响应配置。
- [x] 1.2 新增 `FaultRuntimeState`，保存当前 profile、暂停状态、一次性 DoIP header 损坏和下一次 UDS 覆盖状态。
- [x] 1.3 增加 fault profile 校验：延迟非负、payload length delta 合法、NRC byte 合法、UDS service ID 合法、自定义响应 bytes 合法。
- [x] 1.4 为默认配置补齐 fault profile 默认禁用状态。
- [x] 1.5 记录 fault profile 更新和一次性策略消费事件。

**Quality Gate:** 单元测试覆盖默认 profile、合法 profile、非法 delay/NRC/service/payload length delta/custom response bytes 校验；默认禁用时现有 DoIP/UDS 行为保持不变。

## Phase 2: DoIP fault 注入

- [x] 2.1 在 DoIP 响应写出边界支持 `responseDelayMs`。
- [x] 2.2 在 DoIP/UDS 响应写出边界支持 `pauseResponses`，暂停时保持连接但不发送响应。
- [x] 2.3 实现 Web/API 手动断开目标连接。
- [x] 2.4 实现 `routingActivationFailure`，使 Routing Activation 响应稳定返回失败且连接不被标记为已激活。
- [x] 2.5 实现下一次 DoIP 响应 header `inverseVersion` 损坏。
- [x] 2.6 实现下一次 DoIP 响应 header `payloadLengthDelta` 损坏。

**Quality Gate:** 集成测试覆盖响应延迟、暂停响应超时和恢复、手动断开、Routing Activation 失败、错误 inverse version 和错误 payload length 一次性触发。

## Phase 3: UDS fault 注入

- [x] 3.1 在 UDS 响应边界实现下一次指定 service 的手动 NRC 覆盖。
- [x] 3.2 支持配置 NRC byte，并复用现有 UDS 负响应编码约定。
- [x] 3.3 支持下一次指定 service 的自定义 UDS 响应 bytes。
- [x] 3.4 确保下一次覆盖被消费后自动清除，不影响后续无关请求。
- [x] 3.5 记录 NRC 覆盖和自定义响应触发事件。

**Quality Gate:** 集成测试覆盖下一次指定服务 NRC 覆盖、错误 service 不消费覆盖、自定义 UDS 响应按配置返回。

## Phase 4: WebApi 和 WebConsole 控制

- [x] 4.1 新增 `GET /api/faults`，返回当前 fault profile 和运行时状态摘要。
- [x] 4.2 新增 `PUT /api/faults`，更新 fault profile 并执行校验。
- [x] 4.3 新增 `POST /api/faults/actions/disconnect`，按 connection ID 主动断开连接。
- [x] 4.4 新增 `POST /api/faults/actions/next-nrc`，配置下一次指定 service 的 NRC 覆盖。
- [x] 4.5 在 WebConsole 增加异常注入控制区，支持 enable、响应延迟、暂停/恢复、Routing Activation 失败、header 损坏、断开和 next NRC。
- [x] 4.6 Web 操作失败时显示清晰错误，未引入长期脚本或概率编排 UI。

**Quality Gate:** WebApi 测试覆盖查询、更新、非法配置、断开动作和 next-nrc 动作；WebConsole build 通过。

## Phase 5: Integration & Verification

- [x] 5.1 执行 `openspec validate task-024 --strict`。
- [x] 5.2 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 WebConsole 被修改，执行 `npm run build`。
- [x] 5.5 执行 acceptance criteria check：响应延迟、暂停超时、手动断开、Routing Activation 失败、下一次指定服务 NRC 覆盖。
- [x] 5.6 执行 DoIP header fault check：错误 inverse version 和错误 payload length。
- [x] 5.7 执行 scope check：确认未实现复杂乱序、概率型策略编排、所有 TLS 失败组合或长期故障脚本系统。

**Quality Gate:** OpenSpec 严格校验、build/test、WebConsole build、acceptance criteria 和 scope exclusions 均通过。

## Completion Checklist

- [x] Fault profile 配置模型已实现并校验。
- [x] Web/API 可切换异常策略。
- [x] 响应延迟可被客户端感知。
- [x] 暂停响应会导致客户端超时，恢复后后续请求可响应。
- [x] 手动断开可关闭目标 TCP 连接。
- [x] Routing Activation 失败可稳定复现。
- [x] 错误 inverse version 和错误 payload length 可一次性触发。
- [x] 下一次指定服务可被手动 NRC 或自定义 UDS 响应覆盖。
- [x] 未实现排除范围中的复杂乱序、概率型策略编排、所有 TLS 失败组合或长期故障脚本系统。
- [x] 准备进入独立 Test & Status。
