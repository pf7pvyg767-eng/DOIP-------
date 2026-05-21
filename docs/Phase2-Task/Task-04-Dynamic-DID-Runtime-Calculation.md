# Task 04：动态 DID 运行时计算

## 目标

`0x22` 读取 DID 时返回实时计算值。

## 背景

`ReadDataByIdentifierService` 已经从 `DidRuntimeStore` 读取 DID 值。Phase2 应把动态计算封装在 store/provider 层，让 UDS 服务保持简单。

## 影响文件

- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- `tests/DoipSimulator.Core.Tests/DidRuntimeStoreTests.cs`
- `tests/DoipSimulator.Protocols.Uds.Tests/ReadDataByIdentifierServiceTests.cs`
- `tests/DoipSimulator.Transport.Tests/TcpDoipServerTests.cs`

## 实施内容

- 在 Core 中引入 DID value provider 计算逻辑。
- `DidRuntimeStore.TryRead()` 对 static DID 返回当前固定值。
- `DidRuntimeStore.TryRead()` 对动态 DID 根据当前时间计算数值并编码。
- 为测试引入可控 time provider，避免正弦/线性测试不稳定。
- `ReadDataByIdentifierService` 不需要知道 provider 细节，只从 store 读取 byte array。

## 验收标准

- 同一个 sine DID 在不同时间读取返回不同值。
- random DID 在范围内返回合法编码值。
- linear DID 按时间变化。
- `0x22` 通过 TCP DoIP 读取动态 DID 时返回实时值。

## 建议 OpenSpec Change

`phase2-dynamic-did-provider`
