## Why

Phase2 需要让虚拟 ECU 的 DID 行为从“固定 JSON hex 值”升级为可随时间变化的真实模拟数据。当前 DID 配置和运行时 store 只能表达静态字节，无法满足随机数、正弦波、线性变化等用于诊断仪联调和后续曲线展示的数据源需求。

## What Changes

- 为 DID 配置增加可选 `valueProvider` 对象；未配置时继续等价于现有 static hex DID。
- 支持 `static`、`random`、`sine`、`linear` 四类 provider，其中动态 provider 基于数值配置生成 DID 响应字节。
- 增加 provider 参数：`type`、`numericType`、`min`、`max`、`amplitude`、`offset`、`periodMs`、`slopePerSecond`、`seed`。
- 配置验证按 provider 类型校验必填字段、数值范围和输出长度。
- `DidRuntimeStore` 读取 DID 时按 provider 动态计算当前值，并继续兼容现有写入/持久化静态 DID。
- `sample-config/default.simulator.json` 增加至少两个动态 DID 示例，便于用户直接体验。
- 本变更不实现 Web 端实时绘图、不实现 UI provider 编辑器、不执行脚本表达式。

## Capabilities

### New Capabilities

- `dynamic-did-provider`: 定义 DID value provider 配置、动态值生成、编码长度和验证规则。

### Modified Capabilities

- `configuration-model`: DID 配置模型从固定 hex 值扩展为可选 value provider，并保持静态 DID 兼容。
- `uds-read-data-by-identifier`: `0x22` 读取可返回动态 provider 计算后的当前值，而不再仅限固定字节。
- `did-runtime-write`: DID runtime store 支持静态和动态 DID 的当前值；写入能力仍限定在可写静态 DID，不把动态 DID 当作可持久写入值。

## Impact

- Core configuration model and validation: `src/DoipSimulator.Core/Configuration/SimulatorConfig.cs`, `src/DoipSimulator.Core/Configuration/ConfigValidation.cs`
- DID runtime value resolution: `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- Static and dynamic DID tests: `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs` and configuration validation tests
- Sample configuration: `sample-config/default.simulator.json`
- Existing UDS `0x22` behavior and DID API summaries consume the runtime store and will reflect generated values

## Delta Review

Archive reported a non-blocking warning because this change touched more than 10 spec deltas. The deltas were kept in one change because Task-03 is one cohesive product increment: the configuration contract, validation rules, runtime DID store, `0x22` read behavior, and static-write boundary must land together for dynamic DID providers to be usable without an inconsistent intermediate state.

Future Phase2 dynamic-DID work should be split after this foundation:

- DID realtime charting and sampling API should be a separate Web/API change.
- Provider editing UI should be a separate configuration-management change.
- Additional provider kinds, scaling/units, or ODX/PDX provider import should be separate changes.
