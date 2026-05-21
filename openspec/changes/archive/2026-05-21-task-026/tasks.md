# Implementation Tasks: 性能指标、资源治理和 MVP 验收脚本

**Change ID:** `task-026`

## Phase 1: 指标模型和采集器

- [x] 1.1 新增 `RuntimeMetricsSnapshot` 指标快照模型，包含连接、吞吐、队列、写入速率和进程内存字段。
- [x] 1.2 新增 `RuntimeMetricsCollector` 采集服务，聚合当前连接数、累计接入连接数和核心 UDS 请求 RPS。
- [x] 1.3 采集事件队列长度和 PCAP 队列状态；不可用时返回明确默认值或不可用状态。
- [x] 1.4 采集日志写入速率和 PCAP 写入速率，并使用短窗口计数方式。
- [x] 1.5 采集进程内存快照，包含 working set 和 managed heap。

**Quality Gate:** 单元测试覆盖指标快照聚合、计数器更新、速率计算和缺失队列状态的降级行为。

## Phase 2: Metrics API 和 Web 展示

- [x] 2.1 新增只读 `GET /api/metrics`，返回运行指标 JSON。
- [x] 2.2 API 响应包含 `connections.active`、`connections.totalAccepted`、`throughput.udsRequestsPerSecond`、队列长度、日志/PCAP 写入速率和内存快照。
- [x] 2.3 API 调用不改变运行时、连接、日志、PCAP 或压测状态。
- [x] 2.4 WebConsole 新增基础指标视图，展示连接、吞吐、队列、写入速率和内存。
- [x] 2.5 WebConsole 对缺失或不可用指标提供稳定显示，不宣称企业级监控能力。

**Quality Gate:** API 测试覆盖 `/api/metrics` 返回结构和只读行为；WebConsole build 通过。

## Phase 3: 本地压测工具和自动化验收

- [x] 3.1 在 `tools/loadtest/` 新增简单本地压测脚本。
- [x] 3.2 压测脚本支持维持 20 个并发 TCP 连接。
- [x] 3.3 压测脚本支持对核心 UDS 请求路径发起约 200 请求/秒的短时负载。
- [x] 3.4 压测脚本输出总请求数、成功响应数、失败数、成功率、实际 RPS 和持续时间。
- [x] 3.5 增加可重复验证脚本，覆盖多连接短时压测和核心 UDS 响应正确性。
- [x] 3.6 验证日志和 PCAP 同时开启时核心协议处理不被 UI 或指标采集阻塞的检查步骤已写入文档。

**Quality Gate:** 脚本和文档覆盖 20 并发连接、200 RPS 核心 UDS 请求、成功率摘要和日志/PCAP 同开场景。

## Phase 4: MVP 验收文档和资源治理检查

- [x] 4.1 新增 `docs/MVP-Acceptance-Test-Plan.md`。
- [x] 4.2 文档记录短时压测步骤、预期命令、结果字段和判定方式。
- [x] 4.3 文档记录日志和 PCAP 同时开启时的检查步骤。
- [x] 4.4 文档记录 1 天长稳运行检查项，包括连接稳定性、请求成功率、队列积压、PCAP 文件增长、日志增长和内存快照观察。
- [x] 4.5 文档明确本任务不提供企业级监控、分布式压测或长期指标存储。

**Quality Gate:** 文档可按步骤执行，并与实际压测脚本/API 字段保持一致。

## Phase 5: Integration & Verification

- [x] 5.1 执行 `openspec validate task-026 --strict`。
- [x] 5.2 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 WebConsole 被修改，执行 `npm run build`。
- [x] 5.5 执行 acceptance criteria check：`/api/metrics`、Web 指标、20 并发连接、200 RPS、日志/PCAP 同开、MVP 验收文档。
- [x] 5.6 执行 scope check：确认未实现企业级监控、分布式压测、大功能扩展或大规模重构。
- [x] 5.7 执行 `git diff --check`。

**Quality Gate:** OpenSpec 严格校验、build/test、WebConsole build、acceptance criteria 和 scope exclusions 均通过。

## Completion Checklist

- [x] 运行指标快照和采集器已实现。
- [x] `/api/metrics` 返回基础运行指标。
- [x] WebConsole 展示基础运行指标。
- [x] 简单本地压测脚本已实现。
- [x] 20 并发 TCP 连接验证可由脚本执行。
- [x] 200 RPS 核心 UDS 请求验证可由脚本执行并输出结果摘要。
- [x] 日志和 PCAP 同时开启时核心协议处理不被 UI 阻塞的检查已形成。
- [x] MVP 验收测试文档和长稳运行检查项已形成。
- [x] 未实现排除范围中的企业级监控、分布式压测、大功能扩展或大规模重构。
- [x] 准备进入独立 Test & Status。
