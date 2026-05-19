# Implementation Tasks: PCAP 录制 MVP

**Change ID:** `task-021`

---

## Phase 1: PCAP Writer

- [x] 1.1 在 Observability 层新增 pcap writer，写入标准 pcap global header。
- [x] 1.2 写入 packet record header，包含时间戳、捕获长度和原始长度。
- [x] 1.3 选择并固定 Wireshark 可识别的最小链路类型和 UDP/TCP 数据封装策略。
- [x] 1.4 增加 writer 单元测试，覆盖 global header、packet header、空 payload/非空 payload 和文件关闭行为。

**Quality Gate:**
- [x] pcap writer 单元测试通过。
- [x] 生成文件头可被测试解析为有效 pcap。

---

## Phase 2: Recorder Runtime

- [x] 2.1 新增 pcap recorder 状态模型，包含 `recording`、`filePath`、`bytesWritten`、`maxBytes`。
- [x] 2.2 实现 start/stop/status 录制生命周期。
- [x] 2.3 实现默认输出路径 `logs/pcap/session-<timestamp>.pcap`。
- [x] 2.4 记录 UDP DoIP 收发数据，包含方向、端点、时间戳和 payload。
- [x] 2.5 记录 TCP DoIP/UDS 收发数据，包含方向、端点、时间戳和 payload。
- [x] 2.6 实现 500MiB 上限检查；达到上限时停止录制并发布 `pcap` 分类运行时事件。
- [x] 2.7 确保 recorder 写入失败或停止状态不改变 UDP/TCP/UDS 主流程语义。

**Quality Gate:**
- [x] recorder 生命周期和大小上限单元测试通过。
- [x] scope check 确认未实现 TLS 解密、pcapng、索引搜索或诊断业务新能力。

---

## Phase 3: WebApi

- [x] 3.1 新增 `GET /api/pcap/status`，返回当前录制状态。
- [x] 3.2 新增 `POST /api/pcap/start`，开始录制并返回状态。
- [x] 3.3 新增 `POST /api/pcap/stop`，停止录制并返回状态。
- [x] 3.4 增加 API 测试，覆盖未录制状态、开始录制、重复开始、停止录制和上限状态。

**Quality Gate:**
- [x] WebApi 测试通过。
- [x] API 响应契约包含 `recording`、`filePath`、`bytesWritten`、`maxBytes`。

---

## Phase 4: WebConsole

- [x] 4.1 增加或扩展 Web 页面，显示 PCAP 录制状态。
- [x] 4.2 显示文件路径、已写入字节数、最大字节数和录制中/已停止状态。
- [x] 4.3 提供开始/停止操作入口，调用既有 WebApi。
- [x] 4.4 处理上限到达或错误事件，更新 UI 状态。

**Quality Gate:**
- [x] WebConsole 构建通过。
- [x] UI 不引入 pcap 下载、报文搜索、报文回放或图表分析能力。

---

## Phase 5: Integration & Verification

- [x] 5.1 增加集成测试：开启录制后执行 UDP discovery，验证 pcap 文件非空。
- [x] 5.2 增加集成测试：开启录制后执行 TCP UDS 请求，验证 pcap 文件非空。
- [x] 5.3 验证生成文件可被 Wireshark 打开；如自动化环境无法安装 Wireshark，记录手工验证建议并用 pcap header/packet header 测试作为自动化保障。
- [x] 5.4 执行 `openspec validate task-021 --strict`。
- [x] 5.5 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.6 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.7 执行 scope check：确认未实现 TLS 内容解密、pcapng 高级元数据、报文索引搜索、ODX/PDX、SecurityAccess 插件、异常注入、TLS 传输新能力或诊断业务新能力。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] build/test 通过。
- [x] Acceptance criteria 全部通过。

---

## Completion Checklist

- [x] pcap writer 已实现并有单元测试覆盖。
- [x] UDP/TCP DoIP 收发数据已记录到 pcap。
- [x] start/stop/status API 已实现。
- [x] 500MiB 上限已实现并产生事件。
- [x] Web 控制台显示录制状态。
- [x] 生成文件可被 Wireshark 打开或已有明确手工验证记录。
- [x] 所有 scope exclusions 均已检查。
- [x] 准备进入 `/openspec-apply task-021`。
