# SectionMemory

分区的构建缓存与滚动位置（迁自旧 `Views/SettingsView.fs` 的 `builtSections`、`scrollPositions`、`current`、`Select` 里的记位/恢复规则、`Get/SetSectionScrollOffset`）。控件、滚动容器与导航按钮仍归宿主。分区与构建结果都是类型参数，不认识 SettingsSection。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **每个分区只构建一次**，失效后才重建（重复构建会把同一控件挂到两个父级上）。
2. **切换才记位与恢复**：离开时位置大于 0 或该分区还没记过才记下；切到的分区没记过就回到顶部。

# SPEC
1. [LEMMA: 1, 2] `tests/Tests.fs` 通过。

# 迁入或覆盖的旧测试
旧 SettingsViewTests 的滚动位置用例要起窗口，留旧仓库。本库新增 3 条行为用例。
