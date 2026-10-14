# ShortcutKeys

全局快捷键「按键 → 意图」，迁自旧 `Model/UiControllers.fs` 的 ShortcutRouter。拆成两层：本库只认平台无关的 ShortcutKey（按键名、ctrl 含 ⌘、shift、无修饰）；把 Avalonia 的 KeyEventArgs 映射成 ShortcutKey 留在宿主（`{ key = string e.Key; ctrl = Control||Meta; shift = Shift; noModifiers = KeyModifiers.None }`）。ShortcutAction 的 Escape 等是通用名，按根规则加了 `[<RequireQualifiedAccess>]`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **所有按键与修饰键组合的解析结果与旧代码相同。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由旧 ShortcutRouter.resolve 对 Avalonia 全部按键 × 8 种修饰键组合生成；金标准用例通过。
2. 金标准生成器 `tools/gen-golden.fsx`（跑旧仓库构建出的程序集）；重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 无。CloseWindowTests、ContinueLinkUndoTests 里构造 KeyEventArgs 的用例测的是按键映射层，留在旧仓库。
