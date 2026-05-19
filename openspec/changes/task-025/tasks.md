# Implementation Tasks: ODX/PDX 导入子集

**Change ID:** `task-025`

## Phase 1: Core 导入模型和 ODX 子集解析

- [x] 1.1 新增 `OdxImportReport`，包含 `success`、`imported.entityInfo`、`imported.dids`、`imported.dtcs`、`imported.routines`、`skipped`、`errors`。
- [x] 1.2 新增 ODX 导入结果模型，用于表达可合并到 `SimulatorConfig` 的 ECU 基本信息和 DID 基础信息。
- [x] 1.3 新增 `OdxImportService`，支持从 `.odx` XML 中解析 ECU 基本信息的明确支持子集。
- [x] 1.4 在 `OdxImportService` 中解析 DID 基础信息：DID ID、名称、固定 hex 值或可映射到固定字节值的简单默认值。
- [x] 1.5 将不支持的 ODX 节点或字段写入 `skipped` 列表，包含 path 与 reason。
- [x] 1.6 对格式错误、缺少必要入口或无法解析的文件生成失败报告，不抛出导致服务崩溃的未处理异常。

**Quality Gate:** Core 单元测试覆盖样例 `.odx` DID 解析、ECU 基本信息解析、unsupported 字段 skipped、错误 ODX 失败报告。

## Phase 2: PDX 包读取和入口识别

- [x] 2.1 新增 `PdxPackageReader`，支持读取上传的 `.pdx` 包。
- [x] 2.2 安全解压 `.pdx`，防止路径穿越和不受控文件写入。
- [x] 2.3 识别包内 ODX 入口文件，并将入口交给 `OdxImportService`。
- [x] 2.4 对无入口、多个不明确入口、非法 zip 或超出限制的包生成失败报告。
- [x] 2.5 将包内不支持资源写入 `skipped` 列表。

**Quality Gate:** Core 单元测试覆盖样例 `.pdx` 解压、入口识别、无入口失败、非法包失败和 skipped 记录。

## Phase 3: 配置合并、保存和 `0x22` 验证

- [x] 3.1 新增导入结果到 `SimulatorConfig` 的合并逻辑。
- [x] 3.2 合并 ECU 基本信息时只更新明确支持且合法的字段。
- [x] 3.3 合并 DID 时写入现有 DID 配置结构，并处理重复 DID 的覆盖或合并策略。
- [x] 3.4 合并后执行现有配置校验，非法字段进入错误报告并阻止保存。
- [x] 3.5 支持将导入后的配置保存到现有配置文件路径。
- [x] 3.6 验证导入保存后的 DID 可通过现有 `0x22` ReadDataByIdentifier 读取。

**Quality Gate:** 测试覆盖导入结果保存、重载后 DID 保留、通过 `0x22` 读取导入 DID、非法合并不破坏既有配置。

## Phase 4: WebApi 和 WebConsole

- [x] 4.1 新增 `POST /api/import/odx`，接收 `.odx` 上传并返回导入报告。
- [x] 4.2 新增 `POST /api/import/pdx`，接收 `.pdx` 上传、解压入口并返回导入报告。
- [x] 4.3 API 对错误文件返回失败报告和合适 HTTP 结果，服务进程保持可用。
- [x] 4.4 API 支持明确的保存行为，将合并结果保存到 `SimulatorConfig`。
- [x] 4.5 WebConsole 增加 ODX/PDX 导入入口，支持选择文件、发起上传、展示 imported/skipped/errors。
- [x] 4.6 WebConsole 不提供全量 ODX 编辑、DTC/Routine/Flash 完整解析或第三方兼容认证 UI。

**Quality Gate:** API 测试覆盖 `.odx` 上传、`.pdx` 上传、错误文件上传、失败报告；WebConsole build 通过。

## Phase 5: Integration & Verification

- [x] 5.1 执行 `openspec validate task-025 --strict`。
- [x] 5.2 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 如 WebConsole 被修改，执行 `npm run build`。
- [x] 5.5 执行 acceptance criteria check：样例 `.odx` DID 导入、样例 `.pdx` ECU 信息导入、unsupported 字段 skipped、错误文件失败报告、保存后 `0x22` 读取。
- [x] 5.6 执行 scope check：确认未解析全量 ODX、未完整解析 DTC/Routine/Flash、未做第三方工具链兼容认证。
- [x] 5.7 执行 `git diff --check`。

**Quality Gate:** OpenSpec 严格校验、build/test、必要的 WebConsole build、acceptance criteria 和 scope exclusions 均通过。

## Completion Checklist

- [x] `.odx` 上传和导入子集已实现。
- [x] `.pdx` 上传、解压和入口识别已实现。
- [x] ECU 基本信息和 DID 基础信息解析已实现。
- [x] 导入报告包含 imported、skipped、errors。
- [x] 导入结果可合并并保存到 `SimulatorConfig`。
- [x] 导入后的 DID 可通过 `0x22` 读取。
- [x] 错误文件返回失败报告且服务不崩溃。
- [x] 未实现排除范围中的全量 ODX、完整 DTC/Routine/Flash 解析或第三方工具链兼容认证。
- [x] 准备进入独立 Test & Status。


