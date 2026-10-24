# CommandPalette（组合者）

命令面板检索：PaletteItem/PaletteKind、score、rank、pushRecent、compactKeys、kindLabel、fullText*。协作者 FuzzyMatch（子序列打分）与 PinyinInitials（首字母）；本组合者决定两者的取舍和类别、最近使用加权，不复制它们的规则。命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

最近使用的读写（`loadRecent`/`saveRecent`，依赖 UiPrefs 路径与 LocalText）不属于检索，留在宿主。

## PaletteRecentFile（宿主桥，2026-10-17 增补）

入口外包后宿主就是本仓库：旧宿主 `Model/CommandPalette.fs` 的 `loadRecent` / `saveRecent` / `recentFile` 迁入本组合者（`PaletteRecentFile.fs`，经 `Wanxiang2.UiPrefsFile` 包取 ui.json 同目录）。旧宿主模块名 `PaletteRecent` 与包内同名落盘格式模块冲突，故更名 `PaletteRecentFile`。新增测试覆盖往返、覆盖写与损坏文件退化为空。

# SPEC

- 浏览器发布禁用 JSON 反射时，最近使用记录仍可编码、解码并落盘。`tests/CommandPalette.Tests.fsproj` 显式关闭 `System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault`，沿用字节格式与文件往返断言。2026-10-24 本机对照：原编码实现 2 条失败（包含禁反射异常），改用 `JsonArray` 后 16 条通过；同日在 macOS ARM64 干净发布产物的 PWA 冒烟里实测通过（`Ctrl+K` → 输入「切换主题」→ 回车，`wanxiang.local:palette-recent.json` 记下 `act:theme`）。Linux 宿主实跑仍待验证。

1. 真实组合（两个交付包）下 `tests/Tests.fs` 通过，排序结果与旧实现一致。

# 迁入或覆盖的旧测试
- R20PaletteTests：`373 拼音首字母与子序列都能命中`、`375 空查询先列最近使用，再列操作，不铺全部对话`、`375 最近使用去重、新的在前、有上限`
- 留在旧仓库（需要 Avalonia）：`372 …`、`376 …`、`374 …`

- 新增 `PaletteRecent.decode` / `encode` / `fileName`（旧 PaletteRecent 的落盘格式；文件读写归宿主）：用例 `palette recent file bytes match old load and save` 的期望值由旧程序集实际读写得到。
- 新增壳层面板 `ShellPalette.items`（输入 `ShellPaletteInput`：是否有当前对话、预设、主机能力、主题说法、设置分区、对话列表、是否显示归档）、`commandOf`（条目 id → `PaletteCommand`）、`closesDrawer`，迁自旧 AppShell.PaletteItems / RunPaletteItem。与旧分派的两处差别由宿主补齐：`set:<未知分区>` 旧代码只打开设置页，新代码给 `OpenSettings key`，宿主找不到分区时同样只打开设置页；`preset:<id>` 旧代码先查预设，不存在则 toast「这个预设已经不存在。」且不收抽屉，宿主执行 `ApplyPreset` 时先查。

壳层面板的旧壳金样复刻已退役（金样文件、旧代码副本与金样生成器连同渲染脚本一并删除）。改为直接断言真实行为：`tests/Tests.fs` 给定一份壳层输入事实（有当前对话、两个预设、主机支持预设与导入、六个设置分区、三条对话），断言产出的条目清单与顺序（动作 → 设置分区 → 设置项 → 对话）、每条动作的标题 / 说明 / 检索词 / 快捷键、对话条目的标题与标记副标题、归档对话默认隐藏而打开后才有，以及选中每类条目后派发的 `PaletteCommand` 与哪些命令收起窄屏抽屉。
