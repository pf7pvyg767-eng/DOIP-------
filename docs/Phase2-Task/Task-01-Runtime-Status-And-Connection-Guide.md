# Task 01：UI 运行状态与连接指引

## 目标

用户打开 Web Console 后，第一眼就知道模拟器是否运行，以及诊断仪应该如何连接本系统。

## 背景

当前 Web Console 已经接入真实 API 和 WebSocket 事件，但 Overview 对“如何连接模拟器”的表达不够直接。Phase2 需要把连接信息作为第一屏核心信息，而不是让用户从配置、日志或文档中推断。

## 影响文件

- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`
- `src/DoipSimulator.WebConsole/src/styles.css`

## 实施内容

- Web API 提供 runtime summary，包含 Web API address/port、DoIP UDP/TCP/TLS 端口、TLS 启用状态、VIN、ECU logical address、tester source address whitelist、config path、startedAt、process id、active connection count。
- Web Console Overview 顶部增加“连接本模拟器”信息区。
- UI 用清晰状态表达运行阶段：`API Ready`、`Waiting for DoIP Discovery`、`TCP Connected`、`Routing Activated`、`UDS Traffic Active`。
- Diagnostics 面板继续保留连接与报文详情，Overview 负责给用户最短路径。

## 验收标准

- 启动 Host 后打开 Web Console，不需要阅读文档即可看到诊断仪连接参数。
- 当诊断仪连接并 routing activation 后，状态从等待连接更新为已激活。
- 所有显示数据来自真实 API 或 runtime event。

## 建议 OpenSpec Change

`phase2-runtime-status-and-connection-guide`
