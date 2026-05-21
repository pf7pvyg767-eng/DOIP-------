## Why

Phase2 的目标是让第一阶段功能真正可用并形成顺畅闭环。目前 Host 只能依赖 Ctrl+C 或外部进程终止路径停止运行时，Web Console 无法从 UI 受控停止模拟器，也无法保证 DoIP/Web API 端口在用户操作后被释放。

这会影响日常调试体验：用户进入 Web Console 后可以看到系统状态，但不能从同一个工作流里结束本次模拟运行。

## What Changes

- Host 建立一个可由 Web API 触发的运行时 shutdown 信号，并复用现有优雅退出路径。
- Web API 新增 `POST /api/runtime/shutdown`，用于请求停止当前模拟器运行时。
- shutdown 请求发布 `system.shutdown.requested` 运行时事件，便于 UI 和日志观察。
- 如果 PCAP 正在录制，运行时退出前应停止录制并关闭文件句柄。
- Web Console 在 Dashboard/状态区域新增停止运行时入口，并要求用户确认后才调用 shutdown API。
- Web Console 在确认停止后显示 stopping/disconnected 状态，而不是把后端断开当作普通页面崩溃。
- 不新增 DoIP/UDS 诊断发送能力，不新增配置编辑能力。

## Capabilities

### New Capabilities

- `runtime-shutdown-control`: 覆盖由 Web API/UI 触发的受控运行时停止、端口释放、事件发布和退出前资源收尾。

### Modified Capabilities

- `web-console-dashboard`: Dashboard 从纯只读状态展示扩展为允许唯一的运行时 shutdown 控制，并定义确认、停止中、断开后的 UI 行为。

## Impact

- Host runtime lifecycle: `src/DoipSimulator.Host/Program.cs`
- Web API runtime endpoints: `src/DoipSimulator.WebApi/WebApiApplication.cs`
- Web Console API client and Dashboard/status UI: `src/DoipSimulator.WebConsole/src/api.ts`, `src/DoipSimulator.WebConsole/src/components/StatusPanel.vue`, `src/DoipSimulator.WebConsole/src/views/DashboardView.vue`
- Runtime event publishing and PCAP recorder lifecycle where needed
- Tests under `tests/DoipSimulator.Core.Tests` and focused frontend tests if the existing test harness supports them
