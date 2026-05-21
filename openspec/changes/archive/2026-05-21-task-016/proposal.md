# Proposal: DTC 服务 `0x19`/`0x14` 和 Web 注入

**Change ID:** `task-016`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

当前模拟器已有 UDS 分发、运行时事件、Web 实时观察能力，以及 DID 读写能力，但 DTC 仍缺少可配置、可注入、可查询和可清除的最小闭环。诊断调试人员需要通过 Web 注入或激活 DTC，再用 UDS `0x19` 验证故障读取；也需要通过 UDS `0x14` 清除 DTC，并让 Web 和后续 `0x19` 查询一致反映清除结果。

本 change 只建立 DTC 的 MVP 能力：配置、runtime store、Web 注入/激活/清除、`0x19` 最小子功能、`0x14` 清除，以及 DTC 状态变化事件和日志。不实现真实 ECU 老化、确认、测试失败完整状态机，也不引入 ODX DTC 或其他诊断流程。

## Proposed Solution

- 扩展 DTC 配置模型，用于描述 DTC code、显示名称/描述、初始 status、初始 active 状态等最小字段。
- 新增 DTC runtime store，作为 Web API、WebConsole、UDS `0x19` 和 UDS `0x14` 的单一运行时数据源。
- 新增 Web API：
  - `GET /api/dtcs` 返回当前 DTC runtime 快照。
  - `POST /api/dtcs/{code}/activate` 激活已配置 DTC，可接收最小 status/description 覆盖。
  - `POST /api/dtcs/{code}/clear` 清除已配置 DTC。
- 新增 WebConsole DTC 视图或面板，用于展示 DTC 列表，并触发注入、激活和清除操作。
- 实现 UDS `0x19` ReadDTCInformation MVP 子集，仅覆盖本 change 明确要求的读取路径；未知或未支持子功能返回明确 NRC。
- 实现 UDS `0x14` ClearDiagnosticInformation，以 DTC code 或支持的 group 参数清除 runtime store 中匹配的 DTC。
- DTC 激活、清除和拒绝未知 DTC 操作时，通过现有 `RuntimeEvent`/结构化日志路径记录事件。

## Scope

### In Scope

- DTC 配置和 runtime store。
- Web 注入、激活、清除 DTC。
- API 合同：`GET /api/dtcs`、`POST /api/dtcs/{code}/activate`、`POST /api/dtcs/{code}/clear`。
- UDS `0x19` ReadDTCInformation 的 MVP 子集。
- UDS `0x14` ClearDiagnosticInformation。
- DTC 状态变化事件和日志。
- 单元测试：DTC 激活、清除、查询、未知 DTC 错误。
- API 测试：Web 注入/激活/清除接口。
- 集成测试：Web 激活后通过 DoIP/UDS `0x19` 查询，`0x14` 清除后 Web 和 `0x19` 一致。

### Out of Scope

- 不覆盖 `0x19` 全部子功能。
- 不实现真实老化、确认、测试失败完整状态机。
- 不导入 ODX DTC。
- 不扩大到 SecurityAccess、Routine、Flash 或其他诊断流程。
- 不新增 DTC 持久化、ODX/PDX 转换或诊断数据库导入能力，除非现有配置加载已包含只读 DTC 配置字段。
- 不做无关重构。

## Open Questions

- task 未指定 `0x19` MVP 的精确子功能编号。实现应优先覆盖“读取当前 active DTC 列表和状态”的最小路径，例如按 status mask 读取 DTC 信息；若选择具体子功能编号，应在实现记录中说明。
- task 未指定 `0x14` 是否必须支持 groupOfDTC `FFFFFF`。实现可支持单个 DTC code 清除和/或全组清除，但不得扩展到复杂分组规则。
- task 未指定 DTC status bit 的默认策略。实现应使用配置或 API 提供的 status byte，并避免模拟完整 ISO 状态机。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 增加或完善 DTC 配置模型，描述 code、status、active、description 等最小字段。 |
| Core Runtime State | Yes | 新增 DTC runtime store，统一 Web 与 UDS 的 DTC 当前状态。 |
| Protocols.Uds | Yes | 新增 `0x19` MVP 读取服务和 `0x14` 清除服务，并注册到现有 dispatcher。 |
| WebApi | Yes | 新增 DTC 列表、激活、清除 API。 |
| WebConsole | Yes | 新增或扩展 DTC 管理/注入界面。 |
| Runtime Events/Logs | Yes | DTC 激活、清除、未知操作和 UDS 相关变化进入现有事件/日志。 |
| SecurityAccess/Routine/Flash | No | 不新增这些诊断流程。 |
| Tests | Yes | 增加 Core、UDS、API、集成和 scope check 测试。 |

## Architecture Considerations

- Web API 和 UDS 服务必须共享同一个 DTC runtime store，避免 Web 显示与 `0x19` 查询结果不一致。
- `0x19` 实现必须限定为 MVP 子集；未支持子功能应返回明确负响应，而不是隐式成功或空结果。
- `0x14` 清除必须修改 runtime store，使后续 `GET /api/dtcs` 和 `0x19` 查询都反映清除结果。
- DoIP 层继续只转发 diagnostic payload 到 UDS dispatcher，不解析 DTC 或直接修改 DTC runtime state。
- DTC 事件应复用已有 `RuntimeEvent` 和日志管道，事件数据至少包含 DTC code、操作、active/status 变化、来源和错误原因。
- 未知 DTC 的 Web 操作和 UDS 操作都必须返回明确错误，并保持 runtime store 不变。

## Acceptance Criteria

- [x] Web 激活 DTC 后，`0x19` 可读取该 DTC 及其 status。
- [x] `0x14` 清除后，Web 和 `0x19` 均反映清除结果。
- [x] 未知 DTC 操作返回明确错误，且不改变 DTC runtime store。
- [x] DTC 激活、清除、查询或错误事件进入现有运行时日志。
- [x] Scope check 确认未覆盖 `0x19` 全部子功能、未实现真实老化/确认/测试失败完整状态机、未导入 ODX DTC、未扩大到 SecurityAccess/Routine/Flash 或其他诊断流程。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| `0x19` 子功能范围被扩大 | Medium | High | spec 明确限定 MVP 子集，未支持子功能返回明确 NRC。 |
| Web 和 UDS 使用不同 DTC 数据源 | Medium | High | 要求共享 DTC runtime store，并用 Web 激活后 `0x19` 查询、`0x14` 后 Web 查询做集成测试。 |
| DTC 状态机被过度实现 | Medium | Medium | 明确不实现老化、确认、测试失败完整状态机，只维护 active/status 最小字段。 |
| 清除语义不一致 | Medium | Medium | spec 要求 `0x14` 后 Web 和 `0x19` 一致，并测试 runtime store 状态。 |
| 事件日志缺失 | Low | Medium | 将 DTC 状态变化事件列入验收标准和测试计划。 |
