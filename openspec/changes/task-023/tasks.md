# Implementation Tasks: DLL 安全算法插件 MVP

**Change ID:** `task-023`

---

## Phase 1: ABI 文档和配置契约

- [x] 1.1 新增 `docs/SecurityPlugin-ABI.md`，定义 DLL C ABI、ABI 版本、入口函数、参数、返回码和缓冲区约定。
- [x] 1.2 定义 `DoipSec_GetAbiVersion`、`DoipSec_GenerateSeed`、`DoipSec_VerifyKey` 的成功和失败语义。
- [x] 1.3 新增或补齐 `SecurityPluginConfig`，覆盖 `enabled`、`dllPath`、`timeoutMs`。
- [x] 1.4 增加配置校验：插件启用时校验 DLL 路径和 timeout 合法性。
- [x] 1.5 明确插件错误不得记录 secret key material 或未脱敏敏感数据。

**Quality Gate:**
- [x] ABI 文档覆盖示例函数签名和返回码。
- [x] 配置契约测试覆盖启用、禁用、路径缺失和 timeout 非法。

---

## Phase 2: 插件加载和版本检查

- [x] 2.1 新增 `SecurityPluginLoader`，按配置加载 DLL。
- [x] 2.2 解析 `DoipSec_GetAbiVersion`、`DoipSec_GenerateSeed`、`DoipSec_VerifyKey` 入口函数。
- [x] 2.3 检查 ABI 版本，仅接受 MVP 支持版本。
- [x] 2.4 将 DLL 缺失、入口函数缺失、ABI 不匹配和加载失败转换为清晰错误。
- [x] 2.5 发布或记录插件加载成功和失败事件。

**Quality Gate:**
- [x] 单元测试覆盖 DLL 路径不存在。
- [x] 单元测试覆盖 ABI 版本不匹配。
- [x] 单元测试覆盖入口函数缺失或加载失败时服务不崩溃。

---

## Phase 3: SecurityAccess `0x27` 插件算法路径

- [x] 3.1 新增插件算法适配器，实现现有 SecurityAccess 算法接口或等价边界。
- [x] 3.2 插件启用且加载成功时，seed 请求调用 `DoipSec_GenerateSeed`。
- [x] 3.3 key 请求调用 `DoipSec_VerifyKey`，正确 key 解锁当前 SecurityAccess level。
- [x] 3.4 错误 key 返回现有 `0x27` NRC，并保持安全级别锁定。
- [x] 3.5 插件未启用时保持现有内置算法行为不变。
- [x] 3.6 插件调用失败、输出长度非法或超时时返回清晰错误或 NRC，服务不崩溃。

**Quality Gate:**
- [x] 集成测试验证 `0x27` seed 来自 DLL。
- [x] 集成测试验证正确 key 解锁成功。
- [x] 集成测试验证错误 key 返回 NRC。
- [x] 故障路径测试验证服务不崩溃。

---

## Phase 4: 示例 DLL 工程

- [x] 4.1 新增 `samples/SecurityPluginExample/` 示例 DLL 工程。
- [x] 4.2 示例 DLL 导出 task 指定 ABI 函数。
- [x] 4.3 示例 DLL 使用确定性非 OEM 算法，便于测试计算正确 key。
- [x] 4.4 为示例 DLL 增加构建说明或项目引用，使自动化测试可加载生成产物。

**Quality Gate:**
- [x] 示例 DLL 可构建。
- [x] 示例 DLL 可被 loader 加载。
- [x] 文档明确示例算法不代表真实 OEM 安全算法。

---

## Phase 5: Integration & Verification

- [x] 5.1 执行 `openspec validate task-023 --strict`。
- [x] 5.2 执行 `dotnet build .\DoipSimulator.sln -m:1`。
- [x] 5.3 执行 `dotnet test .\DoipSimulator.sln -m:1`。
- [x] 5.4 如示例 DLL 使用独立工程，执行对应示例 DLL 构建或通过解决方案构建覆盖。
- [x] 5.5 执行 acceptance criteria check：示例 DLL 可加载、seed 来自 DLL、正确 key 解锁、错误 key 返回 NRC、DLL 缺失或 ABI 不匹配服务不崩溃。
- [x] 5.6 执行 scope check：确认未实现进程隔离、脚本插件、企业真实 OEM 算法或完整 `0x84` 加解密。

**Quality Gate:**
- [x] OpenSpec 严格校验通过。
- [x] build/test 通过。
- [x] 示例 DLL 主路径和故障路径测试通过。
- [x] Acceptance criteria 全部通过。

---

## Completion Checklist

- [x] DLL ABI 文档已生成。
- [x] 插件加载和 ABI 版本检查已实现。
- [x] 插件启用时 `0x27` seed/key 使用 DLL 算法。
- [x] 示例 DLL 工程已提供并可加载。
- [x] 插件缺失、ABI 不匹配和调用失败错误清晰且服务不崩溃。
- [x] 所有 scope exclusions 均已检查。
- [x] 准备进入独立 Test & Status。
