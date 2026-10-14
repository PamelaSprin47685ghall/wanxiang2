# UiPrefs

本机界面偏好：ThemePreference、UiPrefs 记录、ui.json 文本 ⇄ 偏好（`parse` / `render`），迁自旧 `Model/UiPrefs.fs`。读写文件（路径、浏览器 localStorage、文件权限）留在宿主；侧栏宽度与桌面最小宽的数值属设计层，经 `UiPrefLimits` 传入。旧 `resolveTheme` 返回设计层的 ThemeMode，这里改为 `isDark`，宿主自己映射。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **ui.json 的字节不变。** [根 AXIOM: 1] 同一份已存文本，读出的偏好与写回的文本都与旧 load/save 相同，不认识的键原样保留。

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧 UiPrefs.load/save 经真实文件跑 `tests/Render.fs` 生成；金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（跑旧仓库构建出的程序集）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- UiPureLogicTests：`ThemePreference conversions and resolveTheme`
- ComposerReliabilityTests：`persisting drafts is on by default and an old ui file without the key keeps it on`
