# Proposal: DID 运行时编辑和 `0x2E` WriteDataByIdentifier

**Change ID:** `task-015`
**Created:** 2026-05-17
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Problem Statement

`task-013` 已实现固定字节 DID 配置和 `0x22` ReadDataByIdentifier，`task-014` 已提供 Web 控制台实时观察能力。当前 DID 值仍缺少运行时编辑闭环：Web 控制台不能修改 DID runtime value，UDS 客户端也不能通过 `0x2E` WriteDataByIdentifier 写入 DID。诊断调试人员需要在不重启或手工改配置的情况下修改可写 DID，并且需要在持久化后重启仍保留新值。

本 change 需要在现有固定字节 DID 模型基础上增加最小可写能力，保持 DID 值的 Web、API、UDS 和 JSON 持久化一致，不引入复杂编码、ODX 写入定义、动态 DID 或其他诊断业务流程。

## Proposed Solution

- 扩展 DID runtime store，使已配置 DID 可暴露当前 runtime value，并支持按 DID 更新固定 hex 字节值。
- 增加 `GET /api/dids`，返回 DID 列表、可写标记、当前值、编码和必要权限摘要。
- 增加 `PUT /api/dids/{did}/value`，接受 `{ "valueEncoding": "hex", "value": "...", "persist": true|false }`，更新 DID runtime value，并在 `persist=true` 时保存到 JSON 配置。
- 在 WebConsole 增加 DID 列表和运行时值编辑 UI，调用上述 API 更新值并刷新展示。
- 新增 UDS `0x2E` WriteDataByIdentifier 服务，写入成功后更新同一 DID runtime store，使后续 `0x22` 和 Web 显示立即看到新值。
- `0x2E` 写入前 SHALL 检查 DID 是否存在、是否允许写入、请求长度是否匹配、当前诊断会话是否满足要求，以及 SecurityAccess 状态是否满足要求。
- 权限检查仅消费现有运行时会话和安全状态；除必要状态读取外，不新增 SecurityAccess seed/key、解锁流程或其他安全业务。
- DID 配置和值持久化继续使用 JSON 配置文件，不引入数据库或 ODX 写入定义。

## Scope

### In Scope

- Web 控制台 DID 列表和运行时值编辑。
- 内部 API 修改 DID runtime value。
- API 合同：`GET /api/dids`、`PUT /api/dids/{did}/value`。
- 实现 UDS `0x2E` WriteDataByIdentifier。
- 支持可写 DID 的会话和安全状态前置条件检查。
- 支持固定 hex 字节值写入，写入后立即影响 `0x22` 读取结果。
- 支持将 DID 配置和值持久化到 JSON。
- 单元测试覆盖写 DID 成功、禁止写、长度错误。
- API 测试覆盖 `PUT /api/dids/{did}/value` 后 `GET /api/dids` 返回更新值。
- 集成测试覆盖 `0x2E` 写入后用 `0x22` 验证。

### Out of Scope

- 不支持复杂编码转换。
- 不支持 ODX 写入定义。
- 不支持动态 DID。
- 不新增 DTC、Routine、Flash 或 SecurityAccess 新业务流程。
- 不实现 SecurityAccess seed/key、解锁算法或权限提升流程；仅允许读取既有安全状态作为写入前置条件。
- 不实现 DID 复杂表达式、脚本计算、长度可变转换或业务语义解析。
- 不做无关重构。

## Open Questions

- 当前 task 未指定 DID 可写配置字段的最终命名。实现应优先复用或最小扩展现有 DID 配置模型，例如 `writable`、`writeSession`、`requiredSecurityLevel`、`length` 等字段；如命名与现有代码冲突，应保持最小兼容并在实现记录中说明。
- 当前 task 未指定 Web 编辑后的默认持久化策略。API 已提供 `persist` 字段；Web UI 应显式提供或固定使用该字段，避免隐式行为不清。
- 当前 task 未指定 DID 写入运行时事件名称。实现可复用现有 `RuntimeEvent` 管道，至少记录 DID ID、写入来源和新值长度。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 扩展 DID 配置以表达可写标记、写入权限前置和当前固定值。 |
| Core Runtime State | Yes | DID runtime store 需要支持读取和更新同一份当前值。 |
| Configuration Persistence | Yes | `persist=true` 时将 DID 配置和值保存回 JSON。 |
| Protocols.Uds | Yes | 新增 `0x2E` WriteDataByIdentifier 服务并注册到 dispatcher。 |
| WebApi | Yes | 新增 `GET /api/dids` 和 `PUT /api/dids/{did}/value`。 |
| WebConsole | Yes | 新增 DID 列表、值展示和编辑提交能力。 |
| Security/Session State | Maybe | 仅读取现有诊断会话和安全状态用于写入权限判断。 |
| DTC/Routine/Flash | No | 不新增相关业务流程。 |
| Tests | Yes | 增加 UDS service、API、Web/数据模型和集成测试。 |

## Architecture Considerations

- Web API 和 `0x2E` 服务必须更新同一个 DID runtime store，避免 Web 与 UDS 读取不同数据源。
- `0x22` ReadDataByIdentifier 应继续从 DID runtime store 读取当前值；因此 Web 或 `0x2E` 写入后，下一次 `0x22` 必须立即返回新值。
- JSON 持久化应复用现有配置 load/save 能力，避免引入数据库或独立存储格式。
- `0x2E` 服务应作为 `IUdsService` 注册到现有 UDS dispatcher，DoIP 层继续只做 diagnostic payload forwarding。
- DID ID 继续保持 16-bit 标识；请求 payload 格式为 service ID `0x2E` 后跟 2 字节 DID 和完整数据值。
- NRC 映射应保持明确：格式或长度错误返回 `0x13`，未配置或不可写 DID 返回 `0x31`，会话不满足返回 `0x22`，安全状态不满足返回 `0x33`。
- 权限检查不得顺手实现新的 SecurityAccess 解锁流程；只检查当前 runtime state 已有安全状态。

## Acceptance Criteria

- [ ] Web 修改 DID 后，`0x22` 立即读到新值。
- [ ] `0x2E` 写入 DID 后，Web 显示新值。
- [ ] 不允许写入的 DID 返回正确 NRC。
- [ ] 写入长度错误返回 `0x13 IncorrectMessageLengthOrInvalidFormat`。
- [ ] 会话前置条件不满足时返回 `0x22 ConditionsNotCorrect`。
- [ ] 安全状态前置条件不满足时返回 `0x33 SecurityAccessDenied`。
- [ ] `persist=true` 持久化后重启仍保留新值。
- [ ] Scope check 确认未实现复杂编码转换、ODX 写入定义、动态 DID、DTC/Routine/Flash/SecurityAccess 新业务流程或无关重构。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Web/API 与 `0x2E` 使用不同 DID 数据源 | Medium | High | 规格要求共用 DID runtime store，并用 API 与 UDS 集成测试验证。 |
| 持久化写回破坏原配置结构 | Medium | Medium | 复用现有 JSON 配置保存路径，测试覆盖 save/reload。 |
| 写入权限被扩大成 SecurityAccess 新业务 | Medium | High | Scope 明确仅检查既有状态，不实现 seed/key 或解锁流程。 |
| NRC 行为不一致 | Medium | Medium | spec 固定 NRC 映射，并要求禁止写、长度、会话、安全状态测试。 |
| DID 值编码被扩展过度 | Medium | Medium | 仅支持固定 hex 字节值，不做复杂编码转换或业务语义解析。 |
