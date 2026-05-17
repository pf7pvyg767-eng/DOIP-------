# Implementation Tasks: DTC 服务 `0x19`/`0x14` 和 Web 注入

**Change ID:** `task-016`

---

## Phase 1: DTC 配置和 Runtime Store

- [x] 1.1 定义或扩展 DTC 配置模型，支持 code、status、active、description/name 等最小字段。
- [x] 1.2 实现 DTC runtime store，提供按 code 查询、激活、清除和快照读取能力。
- [x] 1.3 为未知 DTC、重复 code、无效 code/status 提供清晰验证或运行时错误。
- [x] 1.4 为 DTC 激活、清除和错误路径发布 RuntimeEvent。

**Quality Gate:**
- [x] DTC store 单元测试通过：激活、清除、查询、未知 DTC。
- [x] 事件数据包含 DTC code、操作、来源和状态摘要。

---

## Phase 2: UDS `0x19` 和 `0x14`

- [x] 2.1 实现 `0x19` ReadDTCInformation MVP 子集，并明确拒绝未支持子功能。
- [x] 2.2 实现 `0x14` ClearDiagnosticInformation，清除匹配 DTC 后更新 runtime store。
- [x] 2.3 将 `0x19` 和 `0x14` 注册到现有 UDS dispatcher。
- [x] 2.4 保持 DoIP 层仅负责 diagnostic payload 转发，不写入 DTC 业务逻辑。

**Quality Gate:**
- [x] UDS 单元测试通过：active DTC 查询、清除后查询为空或清除状态、未知 DTC/未支持子功能负响应。
- [x] NRC 映射清晰，错误路径不改变 runtime store。

---

## Phase 3: Web API 和 WebConsole

- [x] 3.1 实现 `GET /api/dtcs`，返回 DTC runtime 快照。
- [x] 3.2 实现 `POST /api/dtcs/{code}/activate`，激活已配置 DTC 并返回更新后状态。
- [x] 3.3 实现 `POST /api/dtcs/{code}/clear`，清除已配置 DTC 并返回更新后状态。
- [x] 3.4 在 WebConsole 增加 DTC 列表、激活/注入和清除操作。
- [x] 3.5 Web API 对未知 DTC 返回明确 HTTP 错误和可读错误信息。

**Quality Gate:**
- [x] API 测试通过：列表、激活、清除、未知 DTC。
- [x] Web 显示状态与 API 快照一致。

---

## Phase 4: 集成验证和 Scope Check

- [x] 4.1 集成测试：Web/API 激活 DTC 后，UDS `0x19` 可读取。
- [x] 4.2 集成测试：UDS `0x14` 清除后，Web/API 和 `0x19` 均反映清除结果。
- [x] 4.3 日志测试：DTC 激活、清除和错误事件进入现有事件/日志管道。
- [x] 4.4 Scope check：确认未实现 `0x19` 全部子功能、真实老化/确认/测试失败完整状态机、ODX DTC 导入、SecurityAccess/Routine/Flash 或其他诊断流程。

**Quality Gate:**
- [x] `openspec validate task-016 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过。
- [x] 如涉及前端变更，`npm run build` 通过。

---

## Completion Checklist

- [x] DTC 配置和 runtime store 已实现。
- [x] Web 注入、激活、清除 DTC 已实现。
- [x] `0x19` MVP 子集已实现。
- [x] `0x14` 清除 DTC 已实现。
- [x] DTC 状态变化事件和日志已实现。
- [x] 验收标准全部通过。
- [x] Scope 和 out_of_scope 均已核对。
- [x] 准备进入独立 Test & Status。
