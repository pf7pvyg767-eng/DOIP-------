# Task 05：DID 采样事件与 API

## 目标

让 Web Console 可以获取 DID 实时值，用于绘图。

## 背景

当前 `uds.did.read` 事件不足以支撑曲线绘制。Phase2 需要为 DID 采样提供事件数据和直接 API。

## 影响文件

- `src/DoipSimulator.Core/Configuration/DidRuntimeStore.cs`
- `src/DoipSimulator.Protocols.Uds/ReadDataByIdentifierService.cs`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`
- `src/DoipSimulator.Core/RuntimeEvents`
- `tests/DoipSimulator.Core.Tests`
- `tests/DoipSimulator.Protocols.Uds.Tests`

## 实施内容

- `uds.did.read` 事件增加 did、raw hex value、numeric value、provider type、sampledAt、connectionId。
- Web API 增加 `GET /api/dids/{did}/sample`，返回当前 DID sample。
- Web API 增加 `GET /api/dids/samples`，返回所有可采样 DID 当前值。
- 对静态非数值 DID，返回 raw hex，不返回 numeric value。

## 验收标准

- 诊断仪读取 DID 后，事件流中出现带数值的 DID sample。
- Web API 可直接读取当前 DID sample。
- 动态 DID 不依赖诊断仪请求也可以被 Web Console 采样显示。

## 建议 OpenSpec Change

`phase2-dynamic-did-provider`
