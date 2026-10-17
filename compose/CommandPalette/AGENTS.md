# CommandPalette（组合者）

命令面板检索：PaletteItem/PaletteKind、score、rank、pushRecent、compactKeys、kindLabel、fullText*。协作者 FuzzyMatch（子序列打分）与 PinyinInitials（首字母）；本组合者决定两者的取舍和类别、最近使用加权，不复制它们的规则。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

最近使用的读写（`loadRecent`/`saveRecent`，依赖 UiPrefs 路径与 LocalText）不属于检索，留在宿主。

## PaletteRecentFile（宿主桥，2026-10-17 增补）

入口外包后宿主就是本仓库：旧宿主 `Model/CommandPalette.fs` 的 `loadRecent` / `saveRecent` / `recentFile` 迁入本组合者（`PaletteRecentFile.fs`，经 `Wanxiang2.UiPrefsFile` 包取 ui.json 同目录）。旧宿主模块名 `PaletteRecent` 与包内同名落盘格式模块冲突，故更名 `PaletteRecentFile`。新增测试覆盖往返、覆盖写与损坏文件退化为空。

# SPEC
1. 真实组合（两个交付包）下 `tests/Tests.fs` 通过，排序结果与旧实现一致。

# 迁入或覆盖的旧测试
- R20PaletteTests：`373 拼音首字母与子序列都能命中`、`375 空查询先列最近使用，再列操作，不铺全部对话`、`375 最近使用去重、新的在前、有上限`
- 留在旧仓库（需要 Avalonia）：`372 …`、`376 …`、`374 …`

- 新增 `PaletteRecent.decode` / `encode` / `fileName`（旧 PaletteRecent 的落盘格式；文件读写归宿主）：用例 `palette recent file bytes match old load and save` 的期望值由旧程序集实际读写得到。
- 新增壳层面板 `ShellPalette.items`（输入 `ShellPaletteInput`：是否有当前对话、预设、主机能力、主题说法、设置分区、对话列表、是否显示归档）、`commandOf`（条目 id → `PaletteCommand`）、`closesDrawer`，迁自旧 AppShell.PaletteItems / RunPaletteItem。`tests/golden/expected.txt` 由 `tools/OldShellPalette.fs`（旧方法体逐行复制，读到的字段与协作者按旧定义给出）在 TZ=UTC 下经 `tools/gen-golden.fsx` 生成。与旧分派的两处差别由宿主补齐：`set:<未知分区>` 旧代码只打开设置页，新代码给 `OpenSettings key`，宿主找不到分区时同样只打开设置页；`preset:<id>` 旧代码先查预设，不存在则 toast「这个预设已经不存在。」且不收抽屉，宿主执行 `ApplyPreset` 时先查。新增用例：`374 没打开对话时不给导出、分叉、存为预设；主机不支持导入时不给导入`、`选中条目：设置项落到分区，对话 id 坏了就什么都不做`。
