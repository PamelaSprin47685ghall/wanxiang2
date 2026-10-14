# ChangeWatch

轮询值的变化检测（迁自旧 `AppShell.fs` `WatchHostViewport` 的 `hostViewportWidth`、`lastResumeEpoch`）。轮询计时、`css > 0` 的过滤与后续动作仍归宿主。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **与上次不同才算变化**，并记下新值。初值可给（恢复代号从 0 起）或不给（视口宽度起初未知）。

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
旧仓库没有直接测这段的用例。本库新增 2 条行为用例。
