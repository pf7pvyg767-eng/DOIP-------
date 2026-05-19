# Proposal: ODX/PDX 导入子集

**Change ID:** `task-025`
**Created:** 2026-05-19
**Status:** Implementation Complete

## Problem Statement

当前模拟器已经具备 `SimulatorConfig` 配置模型、DID 配置、`0x22` ReadDataByIdentifier 读取能力和 WebApi/WebConsole 基础能力，但还没有从外部 ODX/PDX 诊断数据导入到内部配置模型的路径。用户需要用样例 `.odx` 或 `.pdx` 快速导入 ECU 基本信息和 DID 基础信息，并获得可审计的导入报告，而不是手工把所有 DID 写入配置。

本 change 仅覆盖 `task-025` 的 ODX/PDX MVP 导入子集，不实现全量 ODX 工具链。

## Proposed Solution

- 新增 ODX 导入服务，支持上传 `.odx` 文件并解析 ECU 基本信息与 DID 基础信息。
- 新增 PDX 包读取服务，支持上传 `.pdx`、解压并识别包内 ODX 入口文件。
- 新增导入报告模型，记录成功状态、导入的 ECU 信息、导入 DID 数量、跳过项和错误列表。
- 新增 `POST /api/import/odx` 和 `POST /api/import/pdx`，用于提交导入文件并返回导入报告。
- 将支持的导入结果合并到现有 `SimulatorConfig`，使导入后的 DID 可保存并通过已有 `0x22` 路径读取。
- 在 WebConsole 增加导入入口，显示导入报告、skipped 列表和错误信息。

## Scope

### In Scope

- 上传 `.odx`。
- 上传 `.pdx`，解压并识别 ODX 入口。
- 解析 ECU 基本信息，例如名称、逻辑地址、VIN/EID/GID 可映射字段中明确支持的子集。
- 解析 DID 基础信息，例如 DID ID、名称、固定 hex 值或可映射到固定字节值的简单默认值。
- 生成结构化导入报告，包含 `success`、`imported`、`skipped`、`errors`。
- 将导入结果合并到 `SimulatorConfig` 并支持保存。
- 导入后的 DID 可通过既有 `0x22` ReadDataByIdentifier 读取。
- 单元测试、API 测试和集成测试覆盖样例 `.odx`、样例 `.pdx`、错误文件、skipped 列表、配置保存和 `0x22` 读取。

### Out of Scope

- 不解析全量 ODX。
- 不完整解析 DTC、Routine、Flash。
- 不做第三方工具链兼容认证。
- 不实现 ODX 编辑器。
- 不实现 ODX/PDX 导出。
- 不实现复杂 variant coding、诊断会话矩阵、security 解锁规则或 flash 数据转换。
- 不改变既有 `0x22` 服务语义，除非导入配置后按现有 DID 配置读取。

## Acceptance Criteria

- [x] 样例 `.odx` 可导入 DID 基础信息。
- [x] 样例 `.pdx` 可解压并导入 ECU 基本信息。
- [x] 不支持字段进入 `skipped` 列表，并包含 path 与 reason。
- [x] 错误文件返回失败报告，且服务不崩溃。
- [x] 导入结果可保存到 `SimulatorConfig`。
- [x] 导入保存后的 DID 可通过 `0x22` 读取。
- [x] Scope check 确认未解析全量 ODX，未完整解析 DTC、Routine、Flash，未做第三方工具链兼容认证。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Yes | 新增 `OdxImportService`、`PdxPackageReader`、`OdxImportReport`，并复用 `SimulatorConfig` / DID 配置模型。 |
| WebApi | Yes | 新增 `POST /api/import/odx` 和 `POST /api/import/pdx` 上传入口。 |
| WebConsole | Yes | 新增导入视图或导入面板，展示导入结果、skipped 和 errors。 |
| Config | Yes | 合并导入结果到 `SimulatorConfig` 并支持保存。 |
| UDS Runtime | No | 复用既有 DID `0x22` 读取能力，不新增诊断服务语义。 |

## Architecture Considerations

- ODX/PDX 解析逻辑应位于 Core，避免 WebApi 或 WebConsole 直接解析诊断数据库。
- `.pdx` 读取只负责安全解压、入口识别和将 ODX 内容交给 ODX 导入服务。
- 导入服务应以 `SimulatorConfig` 合并结果和 `OdxImportReport` 为边界，避免把未支持的 ODX 结构泄露到运行时协议层。
- XML 解析应使用安全设置，禁止外部实体解析和不受控资源访问。
- PDX 解压必须限制路径穿越、包大小和文件类型，避免恶意压缩包影响工作目录。

## Open Questions

- 无。`task-025` 标记 `needs_clarification` 为无；不确定或未支持的 ODX 字段应进入 `skipped`，不得扩大解析范围。


