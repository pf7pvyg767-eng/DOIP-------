# Spec: 性能指标、资源治理和 MVP 验收脚本

**Change ID:** `task-026`  
**Status:** Draft

## ADDED Requirements

### Requirement: Runtime Metrics Snapshot

系统 SHALL 提供运行指标快照，包含基础模拟器性能和资源指标。

#### Scenario: 返回基础运行指标快照
- **GIVEN** simulator runtime 可用
- **WHEN** 收集 metrics snapshot
- **THEN** snapshot SHALL include active connection count
- **AND** it SHALL include total accepted connection count
- **AND** it SHALL include UDS requests per second or equivalent core UDS throughput metric
- **AND** it SHALL include event queue length
- **AND** it SHALL include pcap queue length or documented synchronous writer state
- **AND** it SHALL include log write rate
- **AND** it SHALL include pcap write rate
- **AND** it SHALL include process memory snapshot data.

#### Scenario: 指标采集不可破坏运行时
- **GIVEN** simulator 正在处理 DoIP 或 UDS traffic
- **WHEN** metrics are collected repeatedly
- **THEN** metrics collection SHALL NOT open or close client connections
- **AND** it SHALL NOT start or stop pcap recording
- **AND** it SHALL NOT start or stop logging
- **AND** it SHALL NOT mutate UDS, DoIP, TLS, DID, DTC, Routine, Flash, ODX/PDX import, SecurityAccess, or fault injection behavior.

#### Scenario: 队列指标不可用时降级
- **GIVEN** a queue length source is not available in the current runtime state
- **WHEN** metrics are collected
- **THEN** the metrics response SHALL remain valid
- **AND** the unavailable queue metric SHALL be represented by a documented default or unavailable state
- **AND** the collector SHALL NOT throw an unhandled exception.

### Requirement: Metrics Web API

WebApi SHALL expose a read-only `GET /api/metrics` endpoint for basic runtime metrics.

#### Scenario: 查询 metrics API
- **GIVEN** WebApi is running
- **WHEN** caller sends `GET /api/metrics`
- **THEN** response SHALL be HTTP 200
- **AND** response body SHALL include `connections.active`
- **AND** response body SHALL include `connections.totalAccepted`
- **AND** response body SHALL include `throughput.udsRequestsPerSecond`
- **AND** response body SHALL include queue metrics
- **AND** response body SHALL include log and pcap write-rate metrics
- **AND** response body SHALL include process memory metrics.

#### Scenario: metrics API 为只读
- **GIVEN** pcap recording, logging, and connected clients have a current state
- **WHEN** `GET /api/metrics` is called one or more times
- **THEN** endpoint SHALL return metrics snapshots
- **AND** it SHALL NOT change pcap recording state
- **AND** it SHALL NOT change logging state
- **AND** it SHALL NOT change connection activation state.

### Requirement: Metrics Web UI

WebConsole SHALL display basic runtime metrics from the metrics API.

#### Scenario: 显示基础运行指标
- **GIVEN** WebConsole is open
- **WHEN** metrics API returns a metrics snapshot
- **THEN** UI SHALL display active connections
- **AND** it SHALL display UDS request throughput
- **AND** it SHALL display event and pcap queue metrics
- **AND** it SHALL display log and pcap write rates
- **AND** it SHALL display process memory information.

#### Scenario: 指标缺失时 UI 稳定显示
- **GIVEN** one or more optional metrics are unavailable
- **WHEN** WebConsole renders the metrics view
- **THEN** UI SHALL keep the page usable
- **AND** it SHALL show a stable empty, zero, or unavailable value for missing metrics
- **AND** it SHALL NOT claim enterprise monitoring, alerting, or long-term metrics storage.

### Requirement: Local Load Test Tool

Repository SHALL include a simple local load-test tool or script for MVP performance verification.

#### Scenario: 维持 20 并发连接
- **GIVEN** simulator is running with TCP DoIP enabled
- **WHEN** load-test tool is run with the MVP concurrency profile
- **THEN** tool SHALL attempt to maintain 20 concurrent TCP connections
- **AND** it SHALL report how many connections were established and maintained
- **AND** it SHALL report connection failures, if any.

#### Scenario: 发送 200 RPS 核心 UDS 请求
- **GIVEN** 20 TCP connections are established and routing activation is completed where required
- **WHEN** load-test tool runs the MVP request-rate profile
- **THEN** tool SHALL send core UDS requests at approximately 200 requests per second
- **AND** it SHALL report total requests
- **AND** it SHALL report successful responses
- **AND** it SHALL report failed or timed-out responses
- **AND** it SHALL report actual achieved RPS and duration.

#### Scenario: 压测工具保持单机简单实现
- **GIVEN** task-026 is implemented
- **WHEN** load-test tool or script is inspected
- **THEN** it SHALL run as a local single-machine tool or test script
- **AND** it SHALL NOT require distributed workers
- **AND** it SHALL NOT require external monitoring infrastructure.

### Requirement: MVP Performance Acceptance

task-026 implementation SHALL verify MVP performance targets through automated or repeatable local checks.

#### Scenario: 20 并发连接验收
- **GIVEN** simulator is running
- **WHEN** MVP performance check is executed
- **THEN** 20 concurrent TCP connections SHALL be maintainable for the configured short test duration
- **AND** failures SHALL be reported in the test output.

#### Scenario: 200 RPS 核心 UDS 验收
- **GIVEN** simulator is running and test DID/session prerequisites are satisfied
- **WHEN** MVP performance check sends core UDS traffic at target rate
- **THEN** check SHALL verify response correctness for supported core UDS request path
- **AND** it SHALL record total requests, success count, failure count, success rate, and achieved RPS
- **AND** acceptance result SHALL be available in test output or load-test output.

#### Scenario: 日志和 pcap 同开不阻塞核心协议处理
- **GIVEN** structured logging is enabled
- **AND** pcap recording is enabled
- **WHEN** MVP performance check sends core UDS traffic
- **THEN** simulator SHALL continue processing core protocol requests
- **AND** UI metrics polling SHALL NOT block core protocol request handling
- **AND** failures or queue buildup SHALL be reported in the check output.

### Requirement: MVP Acceptance Documentation

Repository SHALL include MVP acceptance documentation for performance and long-stability checks.

#### Scenario: 形成 MVP 验收测试文档
- **GIVEN** task-026 is implemented
- **WHEN** `docs/MVP-Acceptance-Test-Plan.md` is opened
- **THEN** document SHALL describe how to run the short performance check
- **AND** it SHALL describe expected result fields
- **AND** it SHALL describe how to inspect `/api/metrics` and the Web metrics view
- **AND** it SHALL describe how to check logging and pcap behavior during the performance run.

#### Scenario: 记录长稳运行检查项
- **GIVEN** task-026 is implemented
- **WHEN** MVP acceptance document is inspected
- **THEN** it SHALL include a 1-day long-stability checklist
- **AND** checklist SHALL include connection stability
- **AND** it SHALL include request success-rate observation
- **AND** it SHALL include queue backlog observation
- **AND** it SHALL include log growth observation
- **AND** it SHALL include pcap file growth observation
- **AND** it SHALL include memory snapshot observation.

### Requirement: Performance Scope Boundaries

task-026 implementation SHALL remain limited to MVP metrics, basic resource checks, and local acceptance scripts.

#### Scenario: 不做企业级监控系统
- **GIVEN** task-026 is implemented
- **WHEN** API, UI, documentation, and dependencies are inspected
- **THEN** change SHALL NOT add alerting rules
- **AND** it SHALL NOT add long-term metrics storage
- **AND** it SHALL NOT add Prometheus, Grafana, OpenTelemetry collector, or external monitoring backend requirements unless introduced by a later validated change.

#### Scenario: 不做分布式压测
- **GIVEN** task-026 is implemented
- **WHEN** load-test tool and tests are inspected
- **THEN** change SHALL NOT add distributed load workers
- **AND** it SHALL NOT add remote orchestration
- **AND** it SHALL NOT require cloud infrastructure to run the MVP check.

#### Scenario: 不新增大功能或大规模重构
- **GIVEN** task-026 is implemented
- **WHEN** code changes are reviewed
- **THEN** change SHALL NOT alter DoIP, UDS, TLS, pcap, logging, DID, DTC, Routine, Flash, ODX/PDX import, SecurityAccess, or fault injection semantics beyond metrics observation and verification hooks
- **AND** it SHALL NOT replace existing runtime architecture with an unrelated implementation.

### Requirement: Performance Verification

task-026 implementation SHALL include focused verification for metrics and MVP acceptance.

#### Scenario: metrics 单元测试
- **GIVEN** unit tests cover the metrics collector
- **WHEN** counters, queues, write rates, and memory values are sampled
- **THEN** tests SHALL verify the metrics snapshot contains expected fields
- **AND** they SHALL verify rate calculations or documented defaults.

#### Scenario: metrics API 测试
- **GIVEN** API tests call `GET /api/metrics`
- **WHEN** endpoint responds
- **THEN** tests SHALL verify the response structure
- **AND** they SHALL verify the endpoint is read-only.

#### Scenario: MVP 压测验证
- **GIVEN** repeatable local load-test tool runs the MVP performance profile
- **WHEN** 20 connections and 200 RPS are exercised
- **THEN** verification SHALL report pass/fail status
- **AND** it SHALL include enough summary data to diagnose connection, throughput, or response-correctness failures.

## MODIFIED Requirements

None.

## REMOVED Requirements

None.
