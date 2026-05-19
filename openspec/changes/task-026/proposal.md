# Proposal: 性能指标、资源治理和 MVP 验收脚本

**Change ID:** `task-026`  
**Created:** 2026-05-19  
**Status:** Implementation Complete  
**Completed:** 2026-05-19

## Problem Statement

当前模拟器已经具备实时观察 UI、日志、PCAP 录制、TLS 传输和核心 UDS/DoIP 能力，但缺少一套可重复的 MVP 性能验收入口。团队需要用基础运行指标判断当前连接数、请求吞吐、队列积压、日志/PCAP 写入速率和内存占用，也需要一个简单压测工具或脚本验证 20 并发连接与 200 RPS 下的核心 UDS 请求响应行为。

本 change 仅覆盖 `task-026` 的基础性能指标、资源治理和 MVP 验收脚本，不建设企业级监控系统，不引入分布式压测或大规模重构。

## Proposed Solution

- 新增运行指标快照模型和采集器，聚合连接数、UDS RPS、事件队列长度、PCAP 队列状态、日志写入速率、PCAP 写入速率和进程内存快照。
- 新增只读 `GET /api/metrics`，返回当前基础运行指标。
- 在 WebConsole 展示基础运行指标。
- 增加本地单机压测脚本，用于验证 20 个并发 TCP 连接和约 200 请求/秒核心 UDS 请求路径。
- 增加 MVP 验收测试文档，包含短时压测和 1 天长稳运行检查项。
- 增加自动化测试覆盖指标聚合、API 返回和只读行为。

## Scope

### In Scope

- 采集当前连接数和累计接入连接数。
- 采集核心 UDS 请求 RPS 或等价吞吐指标。
- 采集事件队列长度和 PCAP 队列状态。
- 采集日志写入速率和 PCAP 写入速率。
- 采集进程内存快照。
- 新增 `GET /api/metrics` 只读 API。
- WebConsole 展示基础运行指标。
- 增加简单本地压测脚本。
- 验证 20 并发 TCP 连接可维持。
- 验证 200 RPS 下核心 UDS 请求响应正确率可由脚本输出判定。
- 验证日志和 PCAP 同时开启时核心协议处理不被 UI 指标轮询阻塞。
- 增加 `docs/MVP-Acceptance-Test-Plan.md`，记录短时压测和 1 天长稳运行检查项。

### Out of Scope

- 不做企业级监控系统。
- 不做分布式压测。
- 不新增大功能。
- 不做大规模重构。
- 不引入外部指标后端、告警系统、Prometheus/Grafana 集成或长期指标数据库。
- 不实现复杂压测编排、云端压测或跨机器协调。
- 不改变 DoIP、UDS、TLS、PCAP 或日志的既有协议语义。

## Acceptance Criteria

- [x] `/api/metrics` 返回运行指标，包含连接、吞吐、队列、日志写入、PCAP 写入和内存快照。
- [x] WebConsole 可展示基础运行指标。
- [x] 20 并发 TCP 连接可由本地压测脚本维持并输出结果。
- [x] 200 RPS 下核心 UDS 请求响应正确率可由本地压测脚本输出判定。
- [x] 日志和 PCAP 同时开启时核心协议处理不被 UI 指标轮询阻塞，相关检查已写入文档和脚本路径。
- [x] 简单压测脚本可被本地执行并输出结果摘要。
- [x] 形成 MVP 验收测试文档，包含长稳运行检查项。
- [x] Scope check 确认未实现企业级监控、分布式压测、大功能扩展或大规模重构。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Observability | Yes | 新增指标快照和采集器，聚合连接、吞吐、队列、写入速率和内存。 |
| WebApi | Yes | 新增只读 `GET /api/metrics`。 |
| WebConsole | Yes | 展示基础运行指标，不引入企业级监控仪表盘。 |
| Tools | Yes | 新增简单本地压测脚本。 |
| Docs | Yes | 新增 MVP 验收测试文档和长稳运行检查项。 |
| Runtime Protocol | No | 不改变 DoIP/UDS/TLS/PCAP 协议语义。 |

## Architecture Considerations

- 指标采集复用既有运行时事件、连接快照、日志和 PCAP 组件的可观察状态，避免重写协议处理路径。
- `GET /api/metrics` 是只读快照 API，不启动或停止运行时、PCAP、日志或压测。
- 写入速率和 RPS 使用轻量短窗口计数器，避免指标采集成为协议处理瓶颈。
- WebConsole 仅展示基础运行指标，不引入复杂图表平台、告警规则或指标历史存储。
- 压测工具是本地单机脚本，目标是 MVP 验收可重复，不扩展为分布式压测框架。
