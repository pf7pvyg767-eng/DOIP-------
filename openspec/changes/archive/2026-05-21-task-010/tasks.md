# Implementation Tasks: TCP 连接管理和路由激活

**Change ID:** `task-010`

---

## Phase 1: 协议契约与连接状态

- [x] 1.1 定义 Routing Activation Request / Response 的最小 payload 契约和响应码常量。
- [x] 1.2 定义 Alive Check Request / Response 的最小 payload 契约。
- [x] 1.3 定义 TCP connection snapshot / connection registry 或等价连接状态模型。
- [x] 1.4 明确 Routing Activation 使用 tester logical source address 做白名单校验。
- [x] 1.5 增加协议层单元测试，覆盖 routing activation 成功响应、失败响应和 Alive Check 响应。

**Quality Gate:**
- [x] 协议单元测试通过。
- [x] 未实现 UDS 业务响应、TLS 或复杂异常注入。

---

## Phase 2: TCP Transport 与 Stream Frame 组包

- [x] 2.1 新增 `TcpDoipServer` 或等价 TCP transport，监听配置的 DoIP TCP 端口。
- [x] 2.2 将 TCP 服务接入 Host 启动和停止生命周期。
- [x] 2.3 新增 `DoipStreamReader` 或等价 frame assembler，支持半包、粘包和连续 frame。
- [x] 2.4 复用 task-008 DoIP frame codec，不重复实现 DoIP 固定 header 解析。
- [x] 2.5 客户端断开、Host 停止或取消时释放 socket、stream 和连接任务。
- [x] 2.6 增加 stream reader 单元测试，覆盖半包、粘包、连续 frame 和无效 frame。

**Quality Gate:**
- [x] Stream reader 测试通过。
- [x] TCP transport 停止后端口和连接资源可释放。

---

## Phase 3: Routing Activation、白名单与 Alive Check

- [x] 3.1 TCP 连接建立时创建连接记录并发布连接创建事件。
- [x] 3.2 收到 Routing Activation Request 时解析 tester logical source address。
- [x] 3.3 tester logical source address 在白名单内时返回成功 Routing Activation Response，并标记连接为 routing activated。
- [x] 3.4 tester logical source address 不在白名单内时返回失败 Routing Activation Response，且不得标记连接为 routing activated。
- [x] 3.5 收到 Alive Check Request 时返回 Alive Check Response。
- [x] 3.6 连接空闲或超时时发布超时事件，并移除连接或标记断开。
- [x] 3.7 客户端主动断开时发布断开事件，并移除连接或标记断开。
- [x] 3.8 增加 TCP client 集成测试，覆盖连接建立、成功激活、白名单失败、Alive Check 和断开清理。

**Quality Gate:**
- [x] Routing Activation 和 Alive Check 测试通过。
- [x] 连接 registry 清理行为通过测试或可验证检查。

---

## Phase 4: 日志可见性、验收与范围检查

- [x] 4.1 发布 `doip` category 的 TCP 连接创建事件。
- [x] 4.2 发布 `doip` category 的 TCP 连接断开事件。
- [x] 4.3 发布 `doip` category 的 TCP 连接超时事件。
- [x] 4.4 发布 `doip` category 的 Routing Activation 成功和失败摘要事件。
- [x] 4.5 发布 `doip` category 的 Alive Check 请求和响应摘要事件。
- [x] 4.6 验证现有结构化日志和 Web 日志 UI 可显示上述事件，无需新增 UI 页面。
- [x] 4.7 运行 `openspec validate task-010 --strict`。
- [x] 4.8 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 4.9 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 4.10 如涉及前端文件，运行对应前端 build；如未涉及，说明无需运行 npm。
- [x] 4.11 执行 scope check：确认未实现 UDS 业务响应、TLS、复杂异常注入、PCAP 或新 Web UI 页面。

**Quality Gate:**
- [x] OpenSpec strict validation 通过。
- [x] Build 和 test 通过。
- [x] Acceptance criteria 全部通过。
- [x] Scope check 通过。

---

## Acceptance Checklist

- [x] 客户端可建立 TCP 连接。
- [x] 合法源地址 Routing Activation 成功。
- [x] 非白名单源地址 Routing Activation 失败。
- [x] 半包情况下 frame 解析正确。
- [x] 粘包情况下多个 frame 解析正确。
- [x] 断开后连接状态从 registry 移除或标记断开。
- [x] 连接创建、断开和超时事件进入结构化日志。
- [x] 连接创建、断开和超时事件可通过现有 Web 日志 UI 看到。
- [x] Alive Check Request 可获得基础响应。
- [x] 未处理 UDS 业务响应。
- [x] 未实现 TLS。
- [x] 未实现复杂异常注入。

## Out of Scope Checklist

- [x] 不处理 UDS 业务响应。
- [x] 不实现 TLS。
- [x] 不实现复杂异常注入。
- [x] 不新增 Web UI 页面或复杂图表。
- [x] 不实现诊断消息转发。
- [x] 不实现 PCAP 或外部 observability 集成。

