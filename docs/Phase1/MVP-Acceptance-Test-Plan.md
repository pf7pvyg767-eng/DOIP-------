# MVP 验收测试计划

本文档记录 `task-026` 的本地性能指标、资源治理和 MVP 验收步骤。它只覆盖单机 MVP 验收，不提供企业级监控、分布式压测、告警规则或长期指标存储。

## 短时性能检查

1. 启动模拟器：

   ```powershell
   dotnet run --project .\src\DoipSimulator.Host\DoipSimulator.Host.csproj -- run --listen-address 127.0.0.1 --port 5080
   ```

2. 确认基础运行指标 API 可读：

   ```powershell
   Invoke-RestMethod http://127.0.0.1:5080/api/metrics
   ```

   返回值应包含：

   - `connections.active`
   - `connections.totalAccepted`
   - `throughput.udsRequestsPerSecond`
   - `queues.event.length`
   - `queues.pcap.length`
   - `writeRates.logEntriesPerSecond`
   - `writeRates.pcapBytesPerSecond`
   - `memory.workingSetBytes`
   - `memory.managedHeapBytes`

3. 打开 WebConsole，确认 Basic metrics 区域可展示连接数、UDS 吞吐、事件队列、PCAP 队列、日志写入速率、PCAP 写入速率和内存快照。缺失指标应显示稳定的空值、零值或 unavailable 状态。

4. 执行本地 .NET 压测工具。默认目标为 20 个 TCP 连接和约 200 请求/秒：

   ```powershell
   dotnet run --project .\tools\loadtest\DoipSimulator.LoadTest\DoipSimulator.LoadTest.csproj -- --host 127.0.0.1 --port 13400 --connections 20 --rps 200 --duration-seconds 10
   ```

   输出 JSON 应包含：

   - `targetConnections`
   - `establishedConnections`
   - `targetRequestsPerSecond`
   - `durationSeconds`
   - `totalRequests`
   - `successfulResponses`
   - `failedResponses`
   - `successRate`
   - `achievedRequestsPerSecond`

   仓库中也保留了 `tools/loadtest/run-mvp-loadtest.ps1` 作为 PowerShell 版本的轻量脚本；正式 200 RPS 验收优先使用 .NET 工具，以降低脚本解释器开销对吞吐的影响。

5. 判定方式：

   - `establishedConnections` 应达到 20。
   - `failedResponses` 应为 0，或由测试记录明确说明失败是否来自配置前置条件。
   - `successRate` 应达到本次发布约定阈值；当前 MVP 建议短时本地验收使用 `1.0`。
   - `achievedRequestsPerSecond` 应接近 200；本地机器资源不足时应记录实际值和环境说明。
   - `/api/metrics` 在压测期间可持续返回 HTTP 200，且不会启动或停止连接、日志或 PCAP。

## 日志和 PCAP 同时开启检查

1. 启动模拟器并打开 WebConsole。
2. 通过 WebConsole 或 API 启动 PCAP 录制。
3. 保持事件日志写入启用。
4. 执行短时压测脚本。
5. 压测期间轮询 `/api/metrics`，观察：

   - `writeRates.logEntriesPerSecond` 有稳定读数。
   - `writeRates.pcapBytesPerSecond` 在有流量且 PCAP 开启时上升。
   - `queues.event.length` 和 `queues.pcap.length` 不出现持续异常积压。
   - 核心 UDS 请求仍返回正确响应，UI 指标轮询不阻塞协议处理。

## 1 天长稳运行检查项

长稳运行建议在固定配置、固定日志目录和固定 PCAP 输出目录下执行，记录开始时间、结束时间、机器规格和配置文件路径。

- 连接稳定性：定期记录 `connections.active` 和 `connections.totalAccepted`，确认连接数量符合预期，没有异常断连或无法释放的连接。
- 请求成功率：定期运行短时核心 UDS 检查，记录 `totalRequests`、`successfulResponses`、`failedResponses` 和 `successRate`。
- 队列积压：定期记录 `queues.event.length` 和 `queues.pcap.length`，确认没有持续增长的积压。
- 日志增长：记录事件日志文件大小增长，确认日志可持续写入且没有写入异常。
- PCAP 文件增长：PCAP 开启时记录 `writeRates.pcapBytesPerSecond`、PCAP 文件路径和文件大小增长，确认达到大小限制时行为符合配置。
- 内存快照：定期记录 `memory.workingSetBytes` 和 `memory.managedHeapBytes`，观察是否存在持续不可解释增长。
- API 可用性：定期调用 `/api/health` 和 `/api/metrics`，确认返回 HTTP 200。

## 范围边界

本验收计划不包含：

- 企业级监控系统。
- 分布式压测。
- Prometheus、Grafana、OpenTelemetry collector 或外部指标后端。
- 告警规则、长期指标数据库或远程压测编排。
- 与性能指标和 MVP 验收无关的 ODX/PDX、异常注入、SecurityAccess、TLS 或协议语义扩展。
