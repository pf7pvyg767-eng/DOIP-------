# Implementation Tasks: DID 运行时编辑和 `0x2E` WriteDataByIdentifier

**Change ID:** `task-015`
**Status:** Implementation Complete
**Completed:** 2026-05-17

---

## Phase 1: DID Runtime Store 与配置持久化

- [x] 1.1 扩展 DID 配置模型，最小表达 DID 是否可写、写入长度、允许诊断会话和所需安全状态。
- [x] 1.2 新增 `DidRuntimeStore`，支持列出 DID、读取当前值、更新当前值，并被 `0x22`、`0x2E` 和 WebApi 共享。
- [x] 1.3 支持固定 hex 字节值校验，拒绝非 hex、奇数长度或与 DID 长度不匹配的值。
- [x] 1.4 支持 `persist=true` 时将 DID 当前值保存到 JSON 配置，`persist=false` 时仅更新运行时值。
- [x] 1.5 保持已有 `0x22` 读取能力从 runtime store 读取当前值。

**Quality Gate:**
- [x] 单元测试覆盖写 DID 成功。
- [x] 单元测试覆盖禁止写 DID。
- [x] 单元测试覆盖长度错误和 hex 格式错误。
- [x] 保存并重新加载后 DID 新值保留。

---

## Phase 2: UDS `0x2E` WriteDataByIdentifier

- [x] 2.1 新增 `WriteDataByIdentifierService` 并注册到现有 UDS dispatcher，服务 ID 为 `0x2E`。
- [x] 2.2 解析 `0x2E` 请求中的 2 字节 DID 和后续写入数据。
- [x] 2.3 对空 payload、缺少 DID、缺少值、长度不匹配或格式错误返回 `0x13 IncorrectMessageLengthOrInvalidFormat`。
- [x] 2.4 对未配置或不可写 DID 返回 `0x31 RequestOutOfRange`。
- [x] 2.5 对诊断会话前置条件不满足返回 `0x22 ConditionsNotCorrect`。
- [x] 2.6 对安全状态前置条件不满足返回 `0x33 SecurityAccessDenied`。
- [x] 2.7 写入成功后返回 `0x6E DID` 正响应，并更新 DID runtime store。

**Quality Gate:**
- [x] UDS 单元测试覆盖正响应 `6E DID`。
- [x] UDS 单元测试覆盖不可写 DID 的 NRC。
- [x] UDS 单元测试覆盖长度错误 NRC。
- [x] UDS 单元测试覆盖会话和安全状态前置条件。

---

## Phase 3: DID Web API

- [x] 3.1 扩展 WebApi，实现 `GET /api/dids`。
- [x] 3.2 `GET /api/dids` 返回 DID ID、名称、`valueEncoding`、当前值、可写标记、长度和权限摘要。
- [x] 3.3 实现 `PUT /api/dids/{did}/value`，接收 `valueEncoding=hex`、`value` 和 `persist`。
- [x] 3.4 API 写入成功后更新同一 DID runtime store，并按 `persist` 决定是否保存 JSON。
- [x] 3.5 API 对未知 DID、不可写 DID、无效 hex、长度错误返回清晰错误响应。

**Quality Gate:**
- [x] API 测试覆盖 `PUT /api/dids/{did}/value` 后 `GET /api/dids` 返回更新值。
- [x] API 测试覆盖不可写 DID 和无效值错误。
- [x] API 测试覆盖 `persist=true` 后重新加载保留新值。

---

## Phase 4: WebConsole DID 编辑 UI

- [x] 4.1 新增 DID 列表组件，调用 `GET /api/dids` 展示 DID。
- [x] 4.2 展示 DID ID、名称、当前 hex 值、可写状态、长度和权限摘要。
- [x] 4.3 为可写 DID 提供 hex 值编辑和提交能力，调用 `PUT /api/dids/{did}/value`。
- [x] 4.4 写入成功后刷新 DID 列表，确保 Web 显示新值。
- [x] 4.5 对 API 返回的禁止写、长度错误或格式错误展示可理解错误，不误报成功。

**Quality Gate:**
- [x] 前端构建通过。
- [x] Web 修改 DID 后后端 `0x22` 可读取到新值的集成路径已由共享 store/API+UDS 测试覆盖。
- [x] UI 不提供复杂编码转换、ODX 写入或动态 DID 控件。

---

## Phase 5: 集成验证与 Scope Check

- [x] 5.1 集成测试覆盖 Web/API 修改 DID 后 `0x22` 立即读到新值。
- [x] 5.2 集成测试覆盖 `0x2E` 写入 DID 后 `GET /api/dids` 返回新值。
- [x] 5.3 集成测试覆盖 `0x2E` 后用 `0x22` 验证新值。
- [x] 5.4 验证持久化后重新加载仍保留新值。
- [x] 5.5 Scope check：未实现复杂编码转换、ODX 写入定义、动态 DID、DTC/Routine/Flash/SecurityAccess 新业务流程或无关重构。

**Quality Gate:**
- [x] `openspec validate task-015 --strict` 通过。
- [x] `dotnet build .\DoipSimulator.sln -m:1` 通过。
- [x] `dotnet test .\DoipSimulator.sln -m:1` 通过。
- [x] `npm run build` 通过。
- [x] `dotnet format` 未执行；该项按规则为非阻塞可选检查。

---

## Completion Checklist

- [x] 已生成 DID runtime store 写入和持久化能力。
- [x] 已实现 `GET /api/dids`。
- [x] 已实现 `PUT /api/dids/{did}/value`。
- [x] 已实现 UDS `0x2E` WriteDataByIdentifier。
- [x] 已实现 Web DID 列表和运行时值编辑。
- [x] 已覆盖写 DID 成功、禁止写、长度错误单元测试。
- [x] 已覆盖 API PUT 后 GET 返回更新值测试。
- [x] 已覆盖 `0x2E` 后 `0x22` 验证集成测试。
- [x] 验收标准全部通过。
- [x] Out of scope 项均未实现。
- [x] 准备进入独立 Test & Status。
