# Proposal: 建立解决方案骨架和开发入口

**Change ID:** `task-001`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

当前仓库只有产品与任务规格文档，尚未建立可编译、可运行、可测试的工程骨架。后续 DoIP、UDS、配置、Web 控制台等任务需要稳定的目录结构、解决方案入口、前端壳、统一脚本和最小 CLI 入口作为共同基础。

## Proposed Solution

按 `MVP-Task-Specs.md` 的默认工程假设建立首个可执行工程骨架：

- 创建 .NET 后端解决方案和基础项目：Host、Core、WebApi。
- 创建 Vue/Vite 前端项目壳：WebConsole。
- 增加统一构建、测试、运行脚本或等价入口。
- 增加 `.gitignore`、基础 README 和目录说明。
- 让后端 Host 提供 `doip-simulator --help` 与 `doip-simulator run` 命令入口，其中 `run` 仅输出占位启动信息。
- 建立可运行的单元测试框架，允许只有占位测试。

## Scope

### In Scope

- 后端 `.NET` solution 和基础项目目录。
- Vue/Vite 前端壳和最小启动入口。
- 统一 build、test、run 开发入口。
- `.gitignore`、README、目录说明等基础仓库文档。
- `doip-simulator --help` 和 `doip-simulator run` 的命令行入口。
- 一个可运行的后端占位单元测试项目。

### Out of Scope

- 不实现 DoIP 协议。
- 不实现 UDS 协议。
- 不实现配置加载、保存或校验。
- 不实现真实 Web 控制台页面或业务交互。
- 不接入数据库。
- 不接入外部服务。
- 不实现健康检查、端口监听、WebApi 启动编排；这些属于后续 task。

## Open Questions

- `task-001` 的 `needs_clarification`：默认工程结构、后端框架和前端脚手架按 `MVP-Task-Specs.md` 的“默认工程假设”执行；如项目负责人另有技术栈要求需在实现前确认。
- 在没有进一步说明时，本 proposal 按默认工程假设继续：.NET 后端解决方案 + Vue/Vite 前端壳。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Backend solution | Yes | 新增 `DoipSimulator.sln`、Host/Core/WebApi 基础项目。 |
| Frontend shell | Yes | 新增 `src/DoipSimulator.WebConsole/` Vue/Vite 项目壳。 |
| Tests | Yes | 新增后端单元测试项目和占位测试。 |
| CLI | Yes | 新增 `doip-simulator --help` 与 `doip-simulator run` 占位入口。 |
| API | No | 不新增业务 API。 |
| Database | No | 不接入数据库。 |
| Protocol logic | No | 不实现 DoIP、UDS 或配置加载逻辑。 |

## Architecture Considerations

- 后端项目目录遵循 `MVP-Task-Specs.md` 默认结构，为后续模块提供稳定边界。
- Host 只承担命令行入口和占位运行输出，不在本 task 中启动真实 WebApi 或协议服务。
- WebApi 项目只作为后续控制面 API 的空壳或基础项目存在，不暴露业务 API。
- WebConsole 只创建 Vue/Vite 项目壳，不实现真实控制台页面。
- Core 项目作为共享契约和后续核心模型的落点，本 task 不引入业务模型。

## Acceptance Criteria

- [ ] 后端解决方案可编译。
- [ ] 前端项目可安装依赖并启动开发服务器。
- [ ] `doip-simulator run` 能启动并打印占位信息。
- [ ] `doip-simulator --help` 能输出命令帮助。
- [ ] 单元测试框架可运行，即使只有一个占位测试。
- [ ] 生成的工程结构与默认工程假设一致，且未引入 DoIP、UDS、配置加载、数据库或外部服务实现。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 技术栈默认假设与负责人期望不一致 | Medium | Medium | 在 open questions 中记录；无进一步说明时按 `MVP-Task-Specs.md` 默认工程假设推进。 |
| 脚手架生成内容过多导致范围扩大 | Medium | Medium | 只保留 task 明确需要的工程壳、脚本、README、占位 CLI 和占位测试。 |
| 前后端入口无法在干净环境运行 | Medium | High | 在实现 checklist 中要求分别验证后端 build/test、前端 build/dev、CLI help/run。 |
