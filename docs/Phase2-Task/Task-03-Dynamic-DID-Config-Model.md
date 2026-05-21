# Task 03：动态 DID 配置模型

## 目标

在配置层支持 DID value provider，同时兼容现有静态 DID。

## 背景

当前 DID 主要依赖 JSON 中的固定 hex 值。Phase2 需要让虚拟 ECU 可以描述动态 DID，例如随机数、正弦波和线性变化值。

## 影响文件

- `src/DoipSimulator.Core/Configuration/SimulatorConfig.cs`
- `src/DoipSimulator.Core/Configuration/ConfigValidation.cs`
- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `sample-config/default.simulator.json`
- `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs`

## 实施内容

- 为 `DidConfig` 增加动态配置字段，例如 `ValueProvider.Type`、`ValueProvider.NumericType`、`ValueProvider.Min`、`ValueProvider.Max`、`ValueProvider.Amplitude`、`ValueProvider.Offset`、`ValueProvider.PeriodMs`、`ValueProvider.SlopePerSecond`、`ValueProvider.Seed`。
- 未配置 `ValueProvider` 时等价于 `static`。
- 配置验证保证：
  - `static` DID 必须有合法 hex value。
  - `random` 必须有 numeric type、min、max。
  - `sine` 必须有 numeric type、amplitude、offset、periodMs。
  - `linear` 必须有 numeric type、offset、slopePerSecond。
  - 动态 DID 的 encoded length 与 numeric type 一致。

## 验收标准

- 现有静态 DID 测试继续通过。
- 新增 random/sine/linear 配置验证测试。
- `sample-config/default.simulator.json` 增加至少 2 个动态 DID 示例。

## 建议 OpenSpec Change

`phase2-dynamic-did-provider`
