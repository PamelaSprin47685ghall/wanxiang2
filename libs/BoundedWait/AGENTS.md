# BoundedWait

请求→回应的有界等待（迁自旧 `Views/SettingsDevices.fs` 的 `BeginLoad`/`NotifyLoadTimeout`/`loading`，与旧 `AppShell.fs` 「检查主机」的 30 秒超时）。计时器与界面提示仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **只有仍在等待时，超时才生效。** [根 AXIOM: 5] 回复先到则超时什么也不做；重新请求重新计时。
2. **上限与说明文字和旧代码逐字相同**（设备 8 秒，检查主机 30 秒）。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
- DevicesUiTests：`device list read without a reply times out with the older host hint`（等待状态与说明；原用例的窗口渲染部分留旧仓库）

# 与旧实现的差异
旧「检查主机」的超时看的是 `settings.Health.Doctor = Running`，不是独立的等待标志。改用本库时，宿主在报告到达时调用 `Settle`；两者在报告到达前后给出的结果相同。
