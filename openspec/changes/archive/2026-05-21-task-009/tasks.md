# Implementation Tasks: UDP 车辆发现和公告

**Change ID:** `task-009`

---

## Phase 1: 协议与配置契约

- [x] 1.1 定义 Vehicle Identification Request、Vehicle Identification Request with EID/VIN、Vehicle Identification Response 和 Vehicle Announcement 的最小 payload 契约。
- [x] 1.2 从 `SimulatorConfig.entity` 投影 VIN、EID、GID、logical address 等车辆识别字段。
- [x] 1.3 明确 Vehicle Announcement 配置来源、默认发送策略和可取消行为。
- [x] 1.4 复用 task-008 DoIP frame codec，不重复实现 header 编解码。
- [x] 1.5 增加协议层单元测试，覆盖响应 payload 字段来自配置。

**Quality Gate:**
- [x] 协议测试通过。
- [x] 未引入 TCP、routing activation、TLS 或 UDS 服务行为。

---

## Phase 2: UDP Transport 与 Host 生命周期

- [x] 2.1 新增 `UdpDoipServer` 或等价 UDP transport，监听配置的 DoIP UDP 端口。
- [x] 2.2 新增 `IDoipUdpHandler` 或等价内部处理接口，接收 `InboundDatagram` 并返回 `OutboundDatagram` 列表。
- [x] 2.3 将 UDP 服务接入 Host 启动流程。
- [x] 2.4 Host 停止或取消时释放 UDP socket 和后台任务。
- [x] 2.5 增加本地 UDP client 集成测试，发送 Vehicle Identification Request 并收到响应。

**Quality Gate:**
- [x] UDP 集成测试通过。
- [x] 端口释放和取消流程通过测试或可验证检查。

---

## Phase 3: Announcement 与事件日志

- [x] 3.1 实现 Vehicle Announcement 按配置发送或定时广播。
- [x] 3.2 Announcement 发送逻辑可取消，不阻塞 Host 停止。
- [x] 3.3 发布 `doip.udp.vehicle_identification.requested` 或等价请求摘要事件。
- [x] 3.4 发布 `doip.udp.vehicle_identification.responded` 或等价响应摘要事件。
- [x] 3.5 发布 `doip.udp.vehicle_announcement.sent` 或等价公告摘要事件。
- [x] 3.6 通过现有日志和 Web UI 事件流验证 `doip` 事件可见。

**Quality Gate:**
- [x] 结构化事件测试通过。
- [x] 现有 Web 日志 UI 无需新增页面即可看到相关事件。

---

## Phase 4: 验收与范围检查

- [x] 4.1 运行 `openspec validate task-009 --strict`。
- [x] 4.2 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 4.3 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 4.4 如涉及前端文件，运行前端构建；如未涉及，说明无需运行 npm。
- [x] 4.5 执行 scope check：确认未实现 TCP、routing activation、TLS、UDS 服务、PCAP 或新 Web UI 页面。
- [x] 4.6 生成或更新 task 状态所需的验证日志。

**Quality Gate:**
- [x] OpenSpec strict validation 通过。
- [x] Build 和 test 通过。
- [x] Scope check 通过。

---

## Acceptance Checklist

- [x] UDP 服务随 Host 启动。
- [x] Host 停止后 UDP 端口被释放。
- [x] 收到车辆识别请求后返回车辆识别响应。
- [x] 车辆识别响应字段来自 `SimulatorConfig.entity`。
- [x] Vehicle Announcement 可按配置发送。
- [x] Web 日志能看到请求和响应摘要。
- [x] DoIP UDP 请求、响应和公告事件写入结构化日志。
- [x] 本地 UDP client 集成测试可以发送请求并收到响应。
- [x] 未实现 TCP。
- [x] 未实现 routing activation。
- [x] 未实现 TLS。

## Out of Scope Checklist

- [x] 不实现 TCP DoIP 监听或 TCP 连接管理。
- [x] 不实现 routing activation。
- [x] 不实现 TLS。
- [x] 不实现 UDS 服务或诊断消息转发。
- [x] 不实现 Alive Check、entity status、power mode 等其他 DoIP 业务。
- [x] 不新增日志搜索、PCAP、外部 observability 集成或复杂 Web UI。


