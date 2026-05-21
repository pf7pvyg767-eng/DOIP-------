# Proposal: 结构化事件模型和文件日志

**Change ID:** `task-006`
**Created:** 2026-05-15
**Status:** Implementation Complete
**Completed:** 2026-05-15

---

## Problem Statement

当前系统已有运行时启动、配置加载/保存和基础 Web 控制台能力，但缺少跨模块共用的结构化运行事件模型和持久化日志通道。后续 Web 观察、诊断与问题排查需要一个统一事件契约，先以文件日志方式记录启动、停止、配置加载和配置保存等关键事件。

本 change 需要在不引入实时 Web 推送、日志查询或协议抓包能力的前提下，建立最小可用的事件发布接口与异步文件日志写入机制，保证日志写入失败不会导致主进程崩溃。

## Proposed Solution

- 在 Core 中定义 `RuntimeEvent` 结构化事件契约，包含事件 ID、时间戳、等级、分类、名称、消息、可选连接 ID 和扩展数据。
- 定义事件发布接口，用于运行时、配置模块和后续模块发布结构化事件。
- 在 Observability/Logging 中实现异步文件事件 sink，将事件以 UTF-8 文本写入日志文件。
- 支持事件分类和等级，用于区分 system、config、connection、doip、uds、state、fault、tls、pcap 等分类及信息/警告/错误等级。
- 在 Host/WebApi/配置接入点发布启动、停止、配置加载和配置保存事件。
- 对日志写入失败进行降级处理：记录或暴露降级错误状态，但不得让主进程崩溃。

## Scope

### In Scope

- 定义 `RuntimeEvent`。
- 定义事件等级和事件分类契约。
- 实现事件发布接口。
- 实现文件日志异步写入。
- 日志文件使用 UTF-8 编码。
- 启动事件写入日志文件。
- 停止事件写入日志文件。
- 配置加载事件写入日志文件。
- 配置保存事件写入日志文件。
- 日志写入失败时不导致主进程崩溃，并提供可观察的降级错误。
- 增加聚焦测试，覆盖事件序列化字段、文件 sink 多事件写入、启动事件日志集成和日志写入失败降级行为。

### Out of Scope

- 不实现 Web 实时推送。
- 不实现高吞吐优化。
- 不实现日志查询。
- 不实现 PCAP 抓包、解析或报文文件写入。
- 不实现日志轮转、压缩、保留策略或索引。
- 不实现分布式追踪或外部日志平台接入。
- 不改变 DoIP/UDS 协议运行时行为。

## Open Questions

- 任务未指定日志文件路径配置来源。实现应采用当前应用配置/启动参数中最小可接入的路径约定；若不存在明确配置项，应使用安全的默认路径并在后续 task 再扩展配置。
- 任务未指定日志文件格式是 JSON Lines 还是其他结构化文本。实现应选择便于逐行追加和测试的 UTF-8 结构化格式，并保持字段与 `RuntimeEvent` 契约一致。
- 任务要求“降级错误”但未指定暴露方式。实现应采用最小机制，例如内存中的 last error、标准错误/宿主日志或 no-op 降级 sink，避免引入日志查询或 Web API。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Yes | 增加 `RuntimeEvent`、事件等级/分类和事件发布接口契约。 |
| Observability | Yes | 增加异步文件事件 sink，使用 UTF-8 写入结构化日志。 |
| Host | Yes | 在启动和停止路径发布 `runtime.started` 与 `runtime.stopped`。 |
| WebApi / Configuration | Maybe | 在配置加载和保存路径发布 `config.loaded` 与 `config.saved`，复用已有配置读写能力。 |
| WebConsole | No | 不实现实时推送、日志视图或查询 UI。 |
| Tests | Yes | 增加 Core/Observability/Host 相关测试覆盖验收标准。 |
| Protocol logic | No | 不新增 DoIP/UDS 行为，不实现 PCAP。 |

## Architecture Considerations

- `RuntimeEvent` 应作为跨模块数据契约放在 Core 或现有共享层，避免 Observability、Host 和 WebApi 各自定义事件结构。
- 事件发布接口应允许调用方以非阻塞方式发布事件，避免日志 IO 泄漏到业务流程。
- 文件日志 sink 应串行化异步写入，保证同一进程内多事件写入不会产生破碎行；本 task 不要求高吞吐优化。
- 事件分类可以包含 `pcap` 作为枚举/分类值，但不得实现 PCAP 抓包、解析、查询或文件输出能力。
- 写入失败应被捕获并转为降级错误，不得从事件发布路径传播导致 Host 或 WebApi 崩溃。
- 配置保存事件应与 task-004 的完整配置保存路径对齐；配置加载事件应与 task-003 的 `ConfigStore` 加载路径对齐，避免重复配置读写逻辑。

## Acceptance Criteria

- [ ] 启动事件写入日志文件。
- [ ] 停止事件写入日志文件。
- [ ] 配置加载事件写入日志文件。
- [ ] 配置保存事件写入日志文件。
- [ ] 日志文件使用 UTF-8 编码。
- [ ] 日志条目包含 `RuntimeEvent` 的核心字段：`id`、`timestamp`、`level`、`category`、`name`、`message`、`connectionId`、`data`。
- [ ] 日志写入失败不会导致主进程崩溃。
- [ ] 日志写入失败时存在可观察的降级错误。
- [ ] Scope check 确认未实现 Web 实时推送、高吞吐优化、日志查询或 PCAP。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 事件系统范围扩大到实时推送或日志查询 | Medium | Medium | 明确仅提供发布接口和文件 sink，不新增 Web API、Web UI 或查询索引。 |
| 日志写入异常影响主流程 | Medium | High | sink 捕获 IO/序列化异常，记录降级错误并保持发布路径不抛出致命异常。 |
| 配置加载/保存事件接入重复配置逻辑 | Low | Medium | 复用现有 `ConfigStore` 和 WebApi 配置保存路径，仅在关键点发布事件。 |
| 异步写入测试不稳定 | Medium | Medium | 提供 flush/dispose 或测试可等待机制，保证测试能确定日志落盘。 |
| PCAP 分类被误解为 PCAP 功能 | Low | Medium | 仅允许分类枚举值，不实现抓包、解析、查询或文件生成。 |

