# Proposal: P2/P2*、TesterPresent 超时和 ResponsePending
**Change ID:** `task-019`
**Created:** 2026-05-17
**Status:** Implementation Complete

---

## Problem Statement

当前模拟器已经具备基础诊断会话、TesterPresent、SecurityAccess、DID、DTC、Routine 和 Web 观察能力，但诊断时序仍停留在最小同步响应模型。诊断客户端常见验证场景需要看到会话 P2/P2* 参数、TesterPresent 超时后的会话回退、服务级响应延迟，以及 `0x78 ResponsePending` 后再返回最终响应的顺序。

本 change 只补齐 task-019 明确要求的 MVP 时序行为，用于验证常见客户端处理逻辑；不引入复杂调度器、概率型延迟或完整 OEM 时序策略。

## Proposed Solution

- 在配置模型中支持会话 P2/P2* 参数，并让 `0x10` DiagnosticSessionControl 正响应返回配置值。
- 为 TesterPresent 增加可配置超时监测：超过超时时间后将非默认会话回退到默认会话，并产生日志或运行时事件。
- 增加服务级响应延迟配置，支持固定初始延迟、固定最终延迟和可选 ResponsePending。
- 当服务配置启用 ResponsePending 时，先返回 `7F SID 78`，再在最终延迟后返回原服务最终响应。
- 在 Web/API 状态中展示定时相关只读状态，例如当前会话、最近 TesterPresent、TesterPresent 超时截止信息或服务延迟状态摘要。
- 使用轻量、可取消、可测试的计时机制；不得扩大为复杂调度器或 OEM 策略引擎。

## Scope

### In Scope

- TesterPresent 超时后回退默认会话。
- TesterPresent 超时回退产生日志或运行时事件。
- `0x10` 会话正响应包含配置的 P2/P2*。
- 服务级固定响应延迟配置。
- 配置 ResponsePending 后先返回 `7F SID 78`，再返回最终响应。
- Web/API 只读展示定时状态。
- 单元测试覆盖超时回退、P2/P2* 编码和 ResponsePending 响应序列。
- 集成测试覆盖客户端收到 `0x78` 与最终响应。
- 并发/非阻塞验证：一个连接的定时行为不得阻塞其他连接基础处理。

### Out of Scope

- 不实现复杂调度器。
- 不实现概率型延迟、抖动、随机延迟或负载相关延迟。
- 不实现完整 OEM 时序策略。
- 不实现完整 ISO-TP 多帧时序、Flash 下载时序或刷写阶段状态机。
- 不新增人工控制延迟的 Web 编辑 UI。
- 不改变 SecurityAccess、DID、DTC、Routine 的业务语义，除非仅为套用服务级响应延迟包装。
- 不实现跨进程、持久化或分布式定时状态。

## Open Questions

- 原始 task 未指定 TesterPresent 超时时长的默认值。实现应选择一个明确、可配置、可测试的默认值，并在测试中使用短超时或可注入时钟避免真实长等待。
- 原始 task 未指定 P2/P2* 配置字段最终命名。实现应复用现有 Session 配置结构；若缺少字段，只做最小数据契约扩展。
- 原始 task 未指定 ResponsePending 应适用于哪些服务。实现应按服务级配置通过 `serviceId` 精确启用，不应默认影响所有服务。
- 原始 task 未指定 Web 展示字段名称。实现应复用现有 ECU 状态快照/API，增加只读定时摘要字段，不引入编辑能力。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core Configuration | Yes | 增加或复用 session P2/P2* 与服务级响应延迟配置。 |
| Core Runtime State | Yes | 维护 TesterPresent 超时截止、最近超时回退和定时状态摘要。 |
| Core Timers | Yes | 增加轻量可取消计时或可注入时钟辅助，不实现复杂调度器。 |
| Protocols.Uds | Yes | DiagnosticSessionControl 使用配置 P2/P2*；响应路径支持 ResponsePending 序列。 |
| Transport/DoIP | Maybe | 需要能够按顺序发送同一 UDS 请求的多个响应，同时不阻塞其他连接。 |
| Web/API | Yes | 暴露并展示定时状态只读摘要。 |
| Tests | Yes | 覆盖超时回退、P2/P2*、ResponsePending 序列、非阻塞行为和 scope check。 |

## Architecture Considerations

- ResponsePending 应作为 UDS 响应调度或包装层能力，尽量复用现有 dispatcher 多响应返回约定；DoIP 层只负责按顺序发送诊断响应，不应理解业务服务逻辑。
- TesterPresent 超时应以 ECU runtime state 为中心更新会话并发布事件，避免在 Web 或 Transport 层复制状态逻辑。
- 计时测试应依赖可注入时钟、短延迟或可控触发机制，避免真实长时间等待导致测试不稳定。
- 服务级延迟配置应为确定性固定值，且默认关闭 ResponsePending，避免改变未配置服务的现有行为。
- Web 只展示状态，不承担定时器驱动和业务决策。

## Acceptance Criteria

- [x] TesterPresent 超时后会话回退到默认会话并产生日志或运行时事件。
- [x] `0x10` 响应包含配置的 P2/P2*。
- [x] 配置 ResponsePending 后先返回 `7F SID 78`，再返回最终响应。
- [x] 定时行为不阻塞其他连接的基础处理。
- [x] Web/API 可以展示定时状态只读摘要。
- [x] Scope check 确认未实现复杂调度器、概率型延迟或完整 OEM 时序策略。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| 计时行为导致测试耗时或竞态 | Medium | Medium | 使用可注入时钟、短延迟配置或可控触发，禁止依赖真实长等待。 |
| ResponsePending 改变未配置服务行为 | Medium | High | 默认关闭，仅按服务级配置启用，并增加未配置服务回归测试。 |
| 多响应发送阻塞其他连接 | Medium | High | 采用异步延迟和可取消任务，集成测试验证其他连接仍可处理基础请求。 |
| Web 展示被扩大为编辑能力 | Low | Medium | proposal 和 tasks 明确只读展示，不新增配置编辑 UI。 |
| 误扩展成 OEM 调度策略 | Low | High | scope/spec/tasks 加入排除项和 scope check。 |
