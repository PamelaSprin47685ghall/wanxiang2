# CommandPalette（组合者）

命令面板检索：PaletteItem/PaletteKind、score、rank、pushRecent、compactKeys、kindLabel、fullText*。协作者 FuzzyMatch（子序列打分）与 PinyinInitials（首字母）；本组合者决定两者的取舍和类别、最近使用加权，不复制它们的规则。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

最近使用的读写（`loadRecent`/`saveRecent`，依赖 UiPrefs 路径与 LocalText）不属于检索，留在宿主。

# SPEC
1. 真实组合（两个交付包）下 `tests/Tests.fs` 通过，排序结果与旧实现一致。

# 迁入或覆盖的旧测试
- R20PaletteTests：`373 拼音首字母与子序列都能命中`、`375 空查询先列最近使用，再列操作，不铺全部对话`、`375 最近使用去重、新的在前、有上限`
- 留在旧仓库（需要 Avalonia）：`372 …`、`376 …`、`374 …`

- 新增 `PaletteRecent.decode` / `encode` / `fileName`（旧 PaletteRecent 的落盘格式；文件读写归宿主）：用例 `palette recent file bytes match old load and save` 的期望值由旧程序集实际读写得到。
