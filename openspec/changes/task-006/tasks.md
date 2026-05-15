# Implementation Tasks: 结构化事件模型和文件日志

**Change ID:** `task-006`

---

## Phase 1: Runtime Event Contracts

- [x] 1.1 在共享核心层定义 `RuntimeEvent`，包含 `id`、`timestamp`、`level`、`category`、`name`、`message`、`connectionId`、`data`。
- [x] 1.2 定义事件等级契约，至少覆盖 `info`、`warning`、`error`。
- [x] 1.3 定义事件分类契约，覆盖 `system`、`config`、`connection`、`doip`、`uds`、`state`、`fault`、`tls`、`pcap`。
- [x] 1.4 增加事件序列化字段完整性测试。

**Quality Gate:**
- [x] `RuntimeEvent` 可被 Host、WebApi、Observability 和测试项目引用。
- [x] 事件分类中的 `pcap` 仅作为分类值存在，未引入 PCAP 功能。

---

## Phase 2: Event Publishing

- [x] 2.1 定义事件发布接口，例如 `IEventBus` 或等价抽象。
- [x] 2.2 提供默认发布实现，将事件分发给已注册 sink。
- [x] 2.3 提供 no-op 或降级发布实现，便于未配置日志时保持运行。
- [x] 2.4 确保发布路径不会因单个 sink 写入失败导致调用方崩溃。

**Quality Gate:**
- [x] 事件发布接口不依赖 Web 实时推送、日志查询或协议模块。
- [x] 写入失败路径有测试覆盖或明确验证。

---

## Phase 3: Asynchronous UTF-8 File Logging

- [x] 3.1 在 Observability/Logging 中实现文件事件 sink。
- [x] 3.2 以异步方式将事件写入日志文件。
- [x] 3.3 使用 UTF-8 编码写入日志文件。
- [x] 3.4 支持连续写入多条事件，并保持每条事件可独立解析或识别。
- [x] 3.5 捕获文件写入失败并记录可观察的降级错误。

**Quality Gate:**
- [x] 文件 sink 多事件写入测试通过。
- [x] UTF-8 编码行为可通过测试或文件读取验证。
- [x] 写入失败不会向主流程传播导致崩溃。

---

## Phase 4: Runtime And Configuration Event Integration

- [x] 4.1 在 Host 启动成功后发布 `runtime.started`，分类为 `system`。
- [x] 4.2 在 Host 停止路径发布 `runtime.stopped`，分类为 `system`。
- [x] 4.3 在配置加载成功后发布 `config.loaded`，分类为 `config`。
- [x] 4.4 在配置保存成功后发布 `config.saved`，分类为 `config`。
- [x] 4.5 增加集成测试：启动服务后日志文件包含 `runtime.started`。

**Quality Gate:**
- [x] 启动、停止、配置加载、配置保存事件均能写入日志文件。
- [x] 未改变配置读写、Web API 或 DoIP/UDS 协议语义。

---

## Phase 5: Verification And Scope Check

- [x] 5.1 执行 `openspec validate task-006 --strict`。
- [x] 5.2 执行 `.NET` build 和测试。
- [x] 5.3 执行 acceptance criteria 手工/自动核对。
- [x] 5.4 执行 scope check，确认未实现 Web 实时推送、高吞吐优化、日志查询或 PCAP。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] 核心测试通过。
- [x] 验收标准全部满足。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] `RuntimeEvent` 已定义。
- [x] 事件发布接口已实现。
- [x] 文件日志异步写入已实现。
- [x] 事件分类和等级已支持。
- [x] 启动事件写入日志文件。
- [x] 停止事件写入日志文件。
- [x] 配置加载事件写入日志文件。
- [x] 配置保存事件写入日志文件。
- [x] 日志文件为 UTF-8。
- [x] 日志写入失败不导致主进程崩溃。
- [x] 日志写入失败时有降级错误。
- [x] 未实现 Web 实时推送。
- [x] 未实现高吞吐优化。
- [x] 未实现日志查询。
- [x] 未实现 PCAP。
- [x] OpenSpec 严格校验已执行。

