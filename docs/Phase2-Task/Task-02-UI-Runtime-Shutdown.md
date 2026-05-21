# Task 02：受控 Runtime Shutdown

## 目标

用户可以从 Web Console 停止模拟器，并释放 DoIP/Web API 端口。

## 背景

当前 Host 主要依赖 Ctrl+C 或进程取消路径停止运行。Phase2 需要补齐从 Web Console 到 Host 的真实 shutdown 闭环。

## 影响文件

- `src/DoipSimulator.Host/Program.cs`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`
- `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- `tests/DoipSimulator.Core.Tests` 或新增 Web API 测试

## 实施内容

- Host 创建 runtime shutdown signal，并传给 Web API。
- Web API 增加 `POST /api/runtime/shutdown`。
- shutdown endpoint 发布 `system.shutdown.requested` 事件。
- 如果 PCAP 正在录制，shutdown 前停止当前 PCAP session。
- Web Console 增加 shutdown 按钮和确认弹窗。
- 触发 shutdown 后 UI 显示“正在停止”，随后显示 API disconnected 状态。

## 验收标准

- 调用 `POST /api/runtime/shutdown` 后 Host 进程退出。
- DoIP TCP/UDP 端口释放。
- Web API 端口释放。
- Web Console shutdown 操作需要确认，避免误点。

## 建议 OpenSpec Change

`phase2-ui-runtime-shutdown`
