# Task 06：Web Console 动态 DID 配置

## 目标

用户能在 Web 端查看 DID 是静态还是动态，并配置动态参数。

## 背景

当前 DID 面板只支持查看和写入 hex 值。Phase2 需要把动态 provider 变成用户可操作能力。

## 影响文件

- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/components/DidEditorPanel.vue`
- `src/DoipSimulator.WebConsole/src/styles.css`
- `src/DoipSimulator.WebApi/WebApiApplication.cs`

## 实施内容

- DID 列表显示 provider 类型。
- 静态 DID 继续显示 hex value 写入表单。
- 动态 DID 显示参数表单：
  - random：numeric type、min、max。
  - sine：numeric type、amplitude、offset、period。
  - linear：numeric type、offset、slope。
- Web API 增加 DID provider 更新接口，例如 `PUT /api/dids/{did}/provider`。
- 更新 provider 后立即影响后续 `0x22` 响应。

## 验收标准

- 在 UI 中把一个 DID 从 static 改为 sine 后，诊断仪读取值开始随时间变化。
- 修改参数后无需重启 Host。
- 非法参数在 UI 中显示明确错误。

## 建议 OpenSpec Change

`phase2-dynamic-did-console`
