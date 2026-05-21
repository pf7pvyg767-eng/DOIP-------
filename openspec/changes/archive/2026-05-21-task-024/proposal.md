# Proposal: 异常注入第一批

**Change ID:** `task-024`
**Created:** 2026-05-19
**Status:** Implementation Complete
**Completed:** 2026-05-19

## Problem Statement

当前模拟器已有 TCP DoIP、Routing Activation、UDS dispatcher、Web 观察和安全插件能力，但缺少可复现、可手动切换的第一批异常注入能力。上位机无法稳定验证延迟、无响应、主动断开、DoIP header 异常、Routing Activation 失败以及 UDS 手动负响应覆盖场景。

本 change 仅覆盖 `task-024` 的异常注入第一批能力。

## Proposed Solution

- 新增 `FaultProfile` 配置模型和 `FaultRuntimeState` 运行时状态，用于表达启用状态、响应延迟、暂停响应、Routing Activation 失败、下一次 DoIP header 损坏、下一次 UDS NRC 或自定义响应覆盖。
- 新增 WebApi fault 控制接口：`GET /api/faults`、`PUT /api/faults`、`POST /api/faults/actions/disconnect`、`POST /api/faults/actions/next-nrc`。
- 在 DoIP 响应写出边界注入响应延迟、暂停响应、Routing Activation 失败和下一次 DoIP header 损坏。
- 在 UDS dispatcher 响应边界注入下一次指定 service 的手动 NRC 或自定义 UDS 响应。
- 在 WebConsole 增加手动异常注入控制区。
- 通过 runtime events 记录 fault profile 更新和故障触发。

## Scope

### In Scope

- Fault profile 配置模型、默认值和校验。
- Fault runtime state 和一次性故障消费语义。
- Web/API 查询、更新和手动触发异常策略。
- 响应延迟。
- 暂停/恢复响应。
- TCP 主动断开。
- Routing Activation 失败。
- 下一次 DoIP 响应 header 的错误 inverse version。
- 下一次 DoIP 响应 header 的错误 payload length。
- 下一次指定 UDS service 的手动 NRC。
- 下一次指定 UDS service 的自定义 UDS 响应 bytes。
- 单元测试和 TCP 集成测试覆盖验收场景。

### Out of Scope

- 不做复杂乱序。
- 不做概率型策略编排。
- 不做所有 TLS 失败组合。
- 不做长期故障脚本系统。
- 不新增 ODX/PDX 导入能力。
- 不新增 SecurityAccess DLL 插件能力。
- 不改变正常路径的既有 TCP/TLS、Routing Activation、UDS dispatcher 语义，除非 fault profile 明确启用对应异常。

## Acceptance Criteria

- [x] 开启响应延迟后，客户端可感知响应延迟。
- [x] 暂停响应后，客户端请求超时；恢复响应后后续请求可继续获得响应。
- [x] 手动断开目标连接后，该 TCP 连接关闭。
- [x] 启用 Routing Activation 失败后，相同请求可稳定复现失败结果，连接不被标记为已激活。
- [x] 配置下一次指定 UDS service NRC 后，该 service 的下一次请求被手动 NRC 覆盖，后续请求恢复正常。
- [x] 错误 inverse version 和错误 payload length 可按一次性策略影响下一次 DoIP 响应 header。
- [x] 自定义 UDS 响应可按下一次指定 service 返回配置的原始响应 bytes。
- [x] Scope check 确认未实现复杂乱序、概率型策略编排、所有 TLS 失败组合或长期故障脚本系统。

## Implementation Notes

- `dotnet format` 未作为阻塞条件执行。
- WebConsole 被修改，已执行 `npm run build`。
