# Implementation Tasks: SecurityAccess 内置算法和安全状态

**Change ID:** `task-018`

---

## Phase 1: 配置模型与验证

- [x] 1.1 扩展 SecurityAccess 配置模型，支持安全等级、seed/key 子功能、算法类型/参数、失败次数上限和锁定时间。
- [x] 1.2 补充配置验证，拒绝重复等级、重复子功能、无效等级、无效算法参数、无效失败次数或无效锁定时间。
- [x] 1.3 如现有 DID/Routine 尚无安全等级要求字段，做最小配置契约扩展以表达受保护访问。

**Quality Gate:**
- [x] 默认配置仍可加载并通过验证。
- [x] SecurityAccess 配置错误返回清晰字段级错误。

---

## Phase 2: 内置算法与安全状态

- [x] 2.1 增加内置示例 seed/key 算法，例如 `builtin-xor` 或 `builtin-add`。
- [x] 2.2 增加每个安全等级的运行时状态：当前 seed、解锁标志、失败计数、锁定截止时间。
- [x] 2.3 确保安全状态为内存状态，不持久化到配置文件。
- [x] 2.4 为算法和状态转换添加单元测试。

**Quality Gate:**
- [x] 正确 key 可由配置算法稳定计算。
- [x] 错误 key 会累计失败次数。
- [x] 锁定截止时间可通过测试稳定验证。

---

## Phase 3: UDS `0x27` SecurityAccess 服务

- [x] 3.1 注册 `0x27` SecurityAccess 服务到现有 UDS dispatcher。
- [x] 3.2 实现请求 seed 主路径，返回非空 seed 和正响应。
- [x] 3.3 实现发送 key 主路径，正确 key 解锁指定安全等级。
- [x] 3.4 实现错误 key NRC、失败计数累计、达到上限后的锁定状态。
- [x] 3.5 实现锁定期间的明确 NRC，并保持状态不被错误请求绕过。
- [x] 3.6 添加 UDS 单元测试和必要的 DoIP dispatcher 集成测试。

**Quality Gate:**
- [x] `0x27` seed/key 正常流程通过。
- [x] 错误 key、锁定和格式错误路径返回明确 NRC。

---

## Phase 4: DID/Routine 保护接入

- [x] 4.1 在 DID 读取服务中检查配置要求的安全等级，未解锁时拒绝受保护 DID。
- [x] 4.2 在 RoutineControl 服务中检查配置要求的安全等级，未解锁时拒绝受保护 Routine。
- [x] 4.3 确认解锁对应安全等级后，受保护 DID/Routine 可按原有正路径成功。
- [x] 4.4 添加未解锁失败、解锁后成功的集成测试。

**Quality Gate:**
- [x] 受保护 DID 未解锁失败、解锁后成功。
- [x] 受保护 Routine 未解锁失败、解锁后成功。
- [x] 未受保护 DID/Routine 行为不回归。

---

## Phase 5: 验证与范围检查

- [x] 5.1 运行 `openspec validate task-018 --strict`。
- [x] 5.2 运行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 运行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 如前端或 Web API 发生变更，运行对应前端 build。
- [x] 5.5 执行 scope check：确认未加载 DLL、未实现 OEM 真实算法、未实现 `0x84`，未扩大到 Flash、ODX/PDX、PCAP/TLS 或其他诊断流程。

**Quality Gate:**
- [x] OpenSpec strict validation 通过。
- [x] 构建和测试通过。
- [x] 所有验收标准通过。
- [x] Scope check 通过。

---

## Completion Checklist

- [x] SecurityAccess 配置模型和验证完成。
- [x] 内置示例算法完成。
- [x] `0x27` seed/key 主路径完成。
- [x] 安全等级解锁状态、失败计数和锁定时间完成。
- [x] 受保护 DID/Routine 访问门控完成。
- [x] 验收测试覆盖完成。
- [x] 未实现任何 out_of_scope 项。
- [x] 准备进入 Test & Status。

