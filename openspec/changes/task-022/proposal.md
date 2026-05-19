# Proposal: TLS 传输和证书配置

**Change ID:** `task-022`
**Created:** 2026-05-19
**Status:** Implementation Complete
**Completed:** 2026-05-19

---

## Problem Statement

当前模拟器已经具备 TCP DoIP 连接、Routing Activation、UDS dispatcher、PCAP 录制和 Web 可观测能力，但还不能按配置启动 DoIP over TLS 主路径，也不能对服务端证书加载、客户端证书校验和 TLS 握手错误进行清晰观测。

本 change 只覆盖 task-022 要求的 TLS 传输和证书配置：在独立 TLS 监听端口上接受 DoIP over TLS 连接，加载服务端证书，按配置校验客户端证书，发布 TLS 连接/错误事件，并在 TLS 连接建立后复用既有 Routing Activation 与 UDS dispatcher。

## Proposed Solution

- 在 Transport 层新增 TLS DoIP server，监听 `network.doipTlsPort`，使用 `SslStream` 或等价 TLS 流完成握手后复用既有 DoIP stream frame 组包、Routing Activation 和 UDS dispatcher 处理路径。
- 在 Security/TLS 边界新增证书加载和客户端证书校验组件，支持服务端 PFX 或现有配置约定中的证书路径，支持 `clientCaPath` 与 `requireClientCertificate`。
- 扩展 TLS 配置契约，覆盖 task 指定的 `enabled`、`serverCertificatePath`、`serverCertificatePassword`、`serverPrivateKeyPath`、`clientCaPath`、`requireClientCertificate` 字段。
- 在运行时事件中发布 `tls` 分类的监听、连接建立、握手成功、握手失败、证书加载失败和客户端证书校验失败事件，错误事件包含可诊断原因。
- 连接状态和 Web UI 中使用 `transport: "tls"` 区分 TLS 连接，现有 TCP 连接继续使用 `transport: "tcp"`。

## Scope

### In Scope

- TLS 监听端口规格设计，默认使用 `network.doipTlsPort`。
- 服务端证书加载规格设计，包含缺失、格式错误或密码错误时的明确错误。
- 客户端证书校验规格设计，包含 `clientCaPath` 与 `requireClientCertificate` 行为。
- 双向认证配置规格设计。
- TLS 连接事件和错误事件规格设计。
- TLS 下复用 DoIP Routing Activation、Alive Check 和 UDS dispatcher 的规格设计。
- Web UI 区分 TCP 和 TLS 连接的规格设计。
- 自动化测试覆盖合法客户端证书、非法/缺失客户端证书、TLS 下 Routing Activation 与 `0x10`/`0x22`/`0x3E`、Web 连接 transport 显示。

### Out of Scope

- 不实现证书生成 UI。
- 不默认解密 PCAP 中 TLS 内容。
- 不实现 `0x84`。
- 不扩大到 ODX/PDX。
- 不实现 SecurityAccess DLL 插件。
- 不实现异常注入或证书错误模拟框架。
- 不实现 pcapng 高级元数据。
- 不实现报文索引搜索。
- 不改变 TCP DoIP 明文连接既有行为。

## Open Questions

- `MVP-Task-Specs.md` 示例中包含 `simulateCertificateError`，但本次明确排除异常注入；本 proposal 将其视为不在 task-022 实现范围内，除非后续 task 单独定义证书错误模拟。
- 服务端证书加载同时出现 `serverCertificatePath`、`serverCertificatePassword` 和既有 `serverPrivateKeyPath`。实现阶段应优先支持 `.pfx` + password 的最小可测路径；如同时支持 PEM 证书加私钥，需保持在证书加载范围内，不扩展到证书生成或管理 UI。
- 原 task 未指定 TLS 协议版本和 cipher policy；实现应采用当前 .NET 平台默认安全 TLS 策略，除非现有项目已有明确配置约定。

## Impact Analysis

| Component | Change Required | Details |
|-----------|-----------------|---------|
| Configuration | Yes | 扩展或补齐 TLS 配置字段，校验 TLS 启用时必要证书配置。 |
| Security | Yes | 新增证书加载和客户端证书校验边界。 |
| Transport | Yes | 新增 TLS DoIP listener，TLS 握手成功后复用 DoIP/TCP 处理流。 |
| DoIP/UDS | Yes | 复用现有 Routing Activation、Alive Check 和 UDS dispatcher，不新增 `0x84`。 |
| Runtime Events | Yes | 发布 `tls` 分类连接、握手、证书和错误事件。 |
| WebApi | Yes | 连接快照或相关状态 API 需要暴露 `transport: "tls"`。 |
| WebConsole | Yes | 连接视图或状态区分 TCP 与 TLS。 |
| PCAP | No | 不默认解密 TLS 内容；PCAP 可继续记录加密后的网络字节。 |
| Tests | Yes | 新增 TLS 证书、mTLS、非法证书、Routing Activation/UDS 和 UI/API transport 测试。 |

## Architecture Considerations

- TLS server 应尽量复用 TCP server 已有的 DoIP frame assembly、Routing Activation、connection registry、UDS dispatcher 和 runtime event 约定，避免复制协议业务逻辑。
- 证书加载和客户端证书校验应位于 Security/TLS 边界，Transport 只消费可用证书和校验结果。
- TLS 握手失败必须以 `tls` 分类事件和结构化错误原因可观测，但不得导致 Host 崩溃或影响明文 TCP listener。
- TLS 连接应作为独立 transport 进入连接 registry，便于 Web UI 和日志区分。
- PCAP 录制在 TLS 场景下只记录加密传输字节，不要求也不默认暴露解密后的 UDS payload。

## Acceptance Criteria

- [ ] 配置合法的服务端证书和合法客户端证书时，客户端可建立 TLS 连接。
- [ ] TLS 连接下可完成 DoIP Routing Activation。
- [ ] TLS 连接下可完成 UDS `0x10`、`0x22`、`0x3E` 请求与响应。
- [ ] 非法客户端证书或缺失必需客户端证书时，TLS 连接失败。
- [ ] TLS 证书或握手失败时，结构化日志/运行时事件包含明确错误原因。
- [ ] Web UI 能将明文 TCP 连接显示为 TCP，将 TLS 连接显示为 TLS。
- [ ] Scope check 确认未实现证书生成 UI、PCAP TLS 解密、`0x84`、ODX/PDX、SecurityAccess DLL 插件、异常注入、pcapng 高级元数据或报文索引搜索。

## Risks & Mitigations

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| TLS 握手错误原因不清晰 | Medium | High | 证书加载和握手失败路径发布 `tls` 分类事件，包含错误码、异常类型或校验失败摘要。 |
| TLS 处理复制 TCP/UDS 业务逻辑导致行为分叉 | Medium | High | TLS listener 握手后复用既有 DoIP stream/Routing Activation/UDS dispatcher 处理路径。 |
| 测试证书生成污染产品能力边界 | Medium | Medium | 测试可使用测试辅助生成临时证书，但产品 UI 不提供证书生成能力。 |
| mTLS 校验过宽导致非法证书被接受 | Medium | High | 单元和集成测试覆盖合法 CA 签发证书、缺失客户端证书和非受信证书失败。 |
| Web 连接 transport 字段破坏既有 TCP 展示 | Low | Medium | 保持 `transport: "tcp"` 兼容，仅为 TLS 连接新增 `transport: "tls"`。 |


