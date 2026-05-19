# Implementation Tasks: TLS 传输和证书配置

**Change ID:** `task-022`

---

## Phase 1: TLS 配置和证书边界

- [x] 1.1 补齐 `TlsConfig` 字段，覆盖 `enabled`、`serverCertificatePath`、`serverCertificatePassword`、`serverPrivateKeyPath`、`clientCaPath`、`requireClientCertificate`。
- [x] 1.2 增加 TLS 配置校验：启用 TLS 时必须提供可加载的服务端证书配置。
- [x] 1.3 新增服务端证书加载组件，返回清晰的缺失文件、格式错误和密码错误。
- [x] 1.4 新增客户端证书校验组件，支持 `clientCaPath` 和 `requireClientCertificate`。
- [x] 1.5 增加证书加载和客户端证书校验单元测试。

**Quality Gate:**
- [x] TLS 配置和证书单元测试通过。
- [x] 错误消息包含字段或证书路径摘要，且不泄露证书密码。

---

## Phase 2: TLS DoIP Transport

- [x] 2.1 新增 TLS DoIP server，监听 `network.doipTlsPort`。
- [x] 2.2 TLS 握手成功后复用既有 DoIP stream frame 组包、Routing Activation、Alive Check 和 UDS dispatcher。
- [x] 2.3 将 TLS 连接注册为 `transport: "tls"`，并保留连接 ID、远端端点、路由激活状态和逻辑地址信息。
- [x] 2.4 发布 TLS listener、连接建立、握手成功、握手失败、证书校验失败和连接关闭事件。
- [x] 2.5 确保 TLS listener 失败或单个 TLS 握手失败不改变明文 TCP listener 行为。

**Quality Gate:**
- [x] TLS transport 集成测试通过。
- [x] scope check 确认未实现 `0x84`、证书生成 UI、TLS PCAP 解密或异常注入。

---

## Phase 3: DoIP/UDS 主路径验证

- [x] 3.1 增加合法客户端证书 TLS 连接测试。
- [x] 3.2 增加 TLS 下 Routing Activation 成功测试。
- [x] 3.3 增加 TLS 下 `0x10` DiagnosticSessionControl 成功测试。
- [x] 3.4 增加 TLS 下 `0x22` ReadDataByIdentifier 成功测试。
- [x] 3.5 增加 TLS 下 `0x3E` TesterPresent 成功测试。
- [x] 3.6 增加非法客户端证书或缺失客户端证书失败测试，并验证日志/事件包含错误原因。

**Quality Gate:**
- [x] TLS 下 Routing Activation 和 UDS 主路径测试通过。
- [x] 非法证书失败路径测试通过。

---

## Phase 4: Web 可见性

- [x] 4.1 确认 WebApi 连接状态契约暴露 `transport` 字段，TLS 连接返回 `tls`。
- [x] 4.2 更新 WebConsole 连接显示，明确区分 TCP 和 TLS 连接。
- [x] 4.3 增加 WebApi 或前端测试，覆盖 TCP/TLS transport 区分。
- [x] 4.4 确认 Web UI 不新增证书生成、证书上传管理或高级 TLS 调试能力。

**Quality Gate:**
- [x] WebConsole 构建通过。
- [x] Web UI transport 区分测试或快照检查通过。

---

## Phase 5: Integration & Verification

- [x] 5.1 执行 `openspec validate task-022 --strict`。
- [x] 5.2 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 执行 `npm run build`，工作目录为 `src\DoipSimulator.WebConsole`。
- [x] 5.5 执行 acceptance criteria check：合法证书连接、TLS Routing Activation、TLS `0x10`/`0x22`/`0x3E`、非法证书失败日志、Web TCP/TLS 区分。
- [x] 5.6 执行 scope check：确认未实现证书生成 UI、PCAP TLS 解密、`0x84`、ODX/PDX、SecurityAccess DLL 插件、异常注入、pcapng 高级元数据或报文索引搜索。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] build/test 通过。
- [x] WebConsole 构建通过。
- [x] Acceptance criteria 全部通过。

---

## Completion Checklist

- [x] TLS 配置和证书加载/校验边界已实现。
- [x] TLS listener 使用 `network.doipTlsPort` 并支持 mTLS 配置。
- [x] TLS 下复用 DoIP Routing Activation 和 UDS dispatcher。
- [x] TLS 连接和错误事件可观测，失败日志包含原因。
- [x] Web UI 可区分 TCP 和 TLS 连接。
- [x] 所有 scope exclusions 均已检查。
- [x] 准备进入 `/openspec-apply task-022`。


