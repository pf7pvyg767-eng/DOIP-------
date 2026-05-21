# Task 07：Web Console DID 实时曲线

## 目标

Web Console 实时显示动态 DID 的当前值和历史变化。

## 背景

动态 DID 的价值需要可视化。用户应该能在 Web Console 中看到正弦、随机和线性 DID 的实时曲线，并用它辅助诊断仪联调。

## 影响文件

- `src/DoipSimulator.WebConsole/src/components/DidEditorPanel.vue`
- 新建 `src/DoipSimulator.WebConsole/src/components/DidLiveChartPanel.vue`
- `src/DoipSimulator.WebConsole/src/api.ts`
- `src/DoipSimulator.WebConsole/src/styles.css`
- `src/DoipSimulator.WebConsole/package.json`

## 实施内容

- 增加 DID 曲线面板。
- 支持选择一个或多个数值 DID。
- 每个 DID 保留最近 60 秒或最近 300 个采样点。
- 优先使用 WebSocket 中的 DID read/sample event 更新曲线。
- 如果没有诊断仪读取，也可以按固定周期调用 sample API，让曲线继续展示模拟值。
- 图表库优先选择轻量依赖；如果不引入依赖，则用 SVG 或 Canvas 绘制简单折线。

## 验收标准

- sine DID 曲线呈周期变化。
- random DID 曲线在配置范围内跳动。
- linear DID 曲线按斜率变化。
- 切换 DID 时图表不崩溃、不显示旧 DID 的错误数据。

## 建议 OpenSpec Change

`phase2-dynamic-did-console`
