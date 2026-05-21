# Proposal: TCP 连接管理和路由激活

**Change ID:** `task-010`
**Created:** 2026-05-16
**Status:** Implementation Complete
**Completed:** 2026-05-16

---

## Problem Statement

项目已具备 DoIP frame codec 和 UDP 车辆发现能力，但诊断客户端仍无法通过 TCP 建立可用的 DoIP 诊断前置链路。后续 UDS 业务处理需要先具备 TCP 监听、连接生命周期管理、stream frame 组包、Routing Activation 和 Alive Check 的基础能力。

本 change 只补齐 TCP 连接管理和路由激活最小闭环：客户端可连接 DoIP TCP 端口，系统可从 TCP stream 中正确还原 DoIP frame，合法源地址可完成 Routing Activation，非白名单源地址被拒绝，连接创建、断开、超时和 Alive Check 相关行为进入现有事件体系。范围不得扩展到 UDS 业务响应、TLS 或复杂异常注入。

## Proposed Solution

- 新增 TCP DoIP server 或等价 transport 组件，随 Host 生命周期监听配置的 DoIP TCP 端口并释放资源。
- 建立连接 registry，记录连接 ID、远端端点、连接时间、routing activation 状态、tester logical address、ECU logical address 和断开状态。
- 新增 TCP stream reader 或等价 frame assembler，基于 task-008 DoIP frame codec 处理半包、粘包和连续 frame。
- 新增 Routing Activation handler，解析请求、校验源地址白名单，并生成成功或失败响应。
- 新增 Alive Check 基础支持，能够响应 Alive Check Request，并在空闲或超时条件下发布连接超时事件或断开连接。
- 将连接创建、断开、超时、routing activation 成功/失败和 Alive Check 事件发布为 `doip` category 的 runtime event，复用现有结构化日志和 Web 日志 UI。
- 增加协议单元测试、stream reader 单元测试、TCP 集成测试和 scope check。

## Scope

### In Scope

- TCP 监听 DoIP 端口。
- TCP 服务随 Host 启动并随 Host 停止释放端口和连接资源。
- 连接创建、断开、超时事件。
- TCP 粘包/半包 frame 组包。
- Routing Activation 请求解析和响应生成。
- 源地址白名单校验。
- Alive Check 基础支持。
- 连接 registry 或等价连接状态管理。
- 连接、routing activation 和 Alive Check 摘要写入现有结构化日志。
- 连接、routing activation 和 Alive Check 摘要可通过现有 Web 日志 UI 看到。
- 增加单元测试、集成测试和 scope check。

### Out of Scope

- 不处理 UDS 业务响应。
- 不实现 TLS。
- 不实现复杂异常注入。
- 不实现 DoIP UDP 车辆发现以外的新 UDP 行为。
- 不实现诊断消息转发或 UDS service handler。
- 不新增 Web UI 页面、复杂图表或连接管理编辑界面。
- 不实现 PCAP、外部 observability 集成或安全审计报表。

## Open Questions

- 原始 task 未明确 Routing Activation 响应码枚举和默认失败码。实现应采用 DoIP 标准或项目既有协议常量中的最小必要响应码，并通过测试固定成功与白名单失败行为。
- 原始 task 未明确 TCP 连接超时时间和 Alive Check 周期字段。实现应优先复用 `SimulatorConfig.network` 或现有配置结构；若需新增最小配置字段，应提供默认值并保持默认配置可启动。
- 原始 task 未明确源地址白名单字段的精确匹配语义。实现应沿用 task-003 已定义的 source address whitelist 配置，明确按 tester logical source address 校验，而不是按 IP 地址校验，除非现有配置另有明确字段。
- 原始 task 的“手动测试：诊断上位机完成路由激活”依赖外部工具。自动验收应以本地 TCP client 集成测试为主，手动工具验证可作为非阻塞补充说明。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Core | Yes | 增加连接 registry 或连接状态模型；可能补充 TCP timeout / Alive Check 最小配置默认值。 |
| Protocols.Doip | Yes | 增加 Routing Activation 和 Alive Check payload 契约、解析和响应生成。 |
| Transport | Yes | 增加 TCP DoIP server、连接接入循环和 TCP stream frame 组包。 |
| Host | Yes | 将 TCP 服务接入 Host 启停生命周期和配置加载。 |
| WebApi | No | 不新增 API；复用现有 runtime event/recent events 能力。 |
| WebConsole | No | 不新增 UI；复用现有日志视图展示 `doip` 事件。 |
| Tests | Yes | 增加 stream reader、routing activation、Alive Check、TCP 集成和 scope check 测试。 |

## Architecture Considerations

- TCP transport 应只负责 socket 生命周期、连接循环和字节流读取；DoIP 协议解析和响应生成应放在协议 handler 中。
- TCP frame 组包必须复用 task-008 DoIP frame codec，避免重复实现固定 header 解析和 payload length 校验。
- Connection registry 应由连接生命周期事件驱动更新，断开或超时后必须移除连接或标记为断开，避免 stale routing session。
- Routing Activation 成功后仅建立诊断前置链路状态，不得处理 UDS payload 或生成 UDS 业务响应。
- 源地址白名单校验应发生在 Routing Activation 阶段，并输出可诊断的失败事件和失败响应。
- Alive Check 应保持基础可用：收到 request 能响应，超时或断开能清理连接；不引入复杂异常注入或高级故障模拟。
- 集成测试应使用本地 loopback 和隔离端口，避免依赖生产 DoIP 端口空闲。

## Acceptance Criteria

- [ ] 客户端可建立 TCP 连接。
- [ ] 合法源地址 Routing Activation 成功。
- [ ] 非白名单源地址 Routing Activation 失败。
- [ ] 半包情况下 frame 解析正确。
- [ ] 粘包情况下多个 frame 解析正确。
- [ ] 断开后连接状态从 registry 移除或标记断开。
- [ ] 连接创建、断开、超时事件进入结构化日志和现有 Web 日志 UI。
- [ ] Alive Check Request 可获得基础响应。
- [ ] Scope check 确认未实现 UDS 业务响应、TLS 或复杂异常注入。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| TCP stream 组包错误导致半包/粘包解析不稳定 | Medium | High | 为 frame assembler 增加覆盖半包、粘包、连续 frame 和非法长度的单元测试。 |
| 连接清理不完整导致端口或 registry 状态泄漏 | Medium | High | 使用 cancellation token 管理连接循环，并测试 Host 停止、客户端断开和超时清理。 |
| Routing Activation 被误扩展到 UDS 业务处理 | Medium | High | 在 spec、tasks 和 scope check 中明确禁止 UDS 业务响应和诊断消息转发。 |
| 源地址白名单语义误解 | Medium | Medium | 在 open questions 和 requirement 中明确按 tester logical source address 校验，并以测试固定行为。 |
| Alive Check 实现过度复杂 | Medium | Medium | 限定为 request/response 和基础超时事件，不实现异常注入或高级故障模拟。 |

