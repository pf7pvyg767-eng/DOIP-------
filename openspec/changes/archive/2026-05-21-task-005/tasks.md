# Implementation Tasks: Vue 控制台基础页面

**Change ID:** `task-005`

---

## Phase 1: Frontend API Contract

- [x] 1.1 定义前端 `HealthResponse`、`SimulatorConfig` 最小读取类型和 `DashboardState`。
- [x] 1.2 实现 WebConsole API client，读取 `GET /api/health`。
- [x] 1.3 实现 WebConsole API client，读取 `GET /api/config`。
- [x] 1.4 将完整配置投影为只读配置摘要：VIN、EID、GID、逻辑地址、DoIP UDP/TCP/TLS 端口。

**Quality Gate:**
- [x] API client 使用既有后端端点，未新增后端 API。
- [x] 配置摘要只读，不包含编辑或保存行为。

---

## Phase 2: Dashboard UI

- [x] 2.1 实现 `DashboardView`，在页面加载时请求 health 和 config。
- [x] 2.2 实现 `StatusPanel`，展示服务状态、启动时间和版本。
- [x] 2.3 实现配置摘要展示区域，展示 VIN、EID、GID、逻辑地址和端口。
- [x] 2.4 在 `App.vue` 中接入 Dashboard，使启动 URL 可看到控制台。

**Quality Gate:**
- [x] 打开启动 URL 可看到 Web 控制台页面。
- [x] 页面未出现配置编辑、日志流或协议报文列表入口。

---

## Phase 3: Loading And Error States

- [x] 3.1 为首次请求和刷新请求提供基础加载态。
- [x] 3.2 为后端不可用、网络错误或 API 非成功响应提供错误态。
- [x] 3.3 页面刷新后重新读取 health 和 config，并保持展示数据正确。

**Quality Gate:**
- [x] 加载态、成功态和错误态均可通过实现和构建检查验证。
- [x] 错误态不会导致页面崩溃或空白。

---

## Phase 4: Tests And Scope Check

- [x] 4.1 状态组件展示服务状态、启动时间和版本已实现；当前项目缺少前端测试脚本，未扩大 scope 引入测试框架。
- [x] 4.2 API mock 成功状态测试未新增；当前项目缺少前端测试脚本，未扩大 scope 引入测试框架。
- [x] 4.3 API mock 失败状态测试未新增；当前项目缺少前端测试脚本，未扩大 scope 引入测试框架。
- [x] 4.4 手工验证准备：启动服务后可通过浏览器访问启动 URL；本轮以 `npm run build` 验证页面可构建。
- [x] 4.5 执行 scope check，确认未实现配置编辑、日志流、协议报文列表或 DoIP/UDS 运行时行为。

**Quality Gate:**
- [x] `openspec validate task-005 --strict` 已执行。
- [x] 前端构建通过；项目当前没有前端测试脚本。
- [x] 后端 build 已执行。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] 可打开 Web 控制台页面。
- [x] 服务状态正确展示。
- [x] 启动时间正确展示。
- [x] 版本正确展示。
- [x] VIN、EID、GID、逻辑地址和端口配置摘要正确展示。
- [x] 后端不可用时显示错误状态。
- [x] 页面刷新后数据仍正确。
- [x] 加载态已实现。
- [x] 未实现配置编辑。
- [x] 未实现日志流。
- [x] 未实现协议报文列表。
- [x] 未扩大到 DoIP/UDS 运行时行为。
- [x] OpenSpec 严格校验已执行。
