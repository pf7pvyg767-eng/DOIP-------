# Task 08：连接指引与动态 DID 的轻量验收

## 目标

只为核心功能增加必要验证，不让测试流程喧宾夺主。

## 背景

Phase2 的重点是完善真实功能。验收脚本应服务于功能，不应把 MSI 安装、完整 UI E2E 和报告系统放进每次开发循环。

## 影响文件

- `runs/local-dev/doip-uds-smoke-temp.ps1`
- 新建或更新 `scripts/phase2-functional-smoke.ps1`
- `README.md`
- `docs/Phase2-Task-Plan.md`

## 实施内容

- smoke 脚本覆盖：
  - API health。
  - runtime summary。
  - UDP discovery。
  - routing activation。
  - `0x22` 读取 static DID。
  - `0x22` 读取 dynamic DID。
  - sample API 返回数值。
  - shutdown API 可用。
- 不把 MSI 安装、完整 UI E2E、完整报告系统放入每次任务测试。

## 验收标准

- `.\scripts\phase2-functional-smoke.ps1` 能验证核心功能。
- 输出清楚指出每个功能是否通过。

## 建议 OpenSpec Change

`phase2-functional-smoke`
