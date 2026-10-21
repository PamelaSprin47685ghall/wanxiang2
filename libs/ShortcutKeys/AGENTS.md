# ShortcutKeys

全局快捷键「按键 → 意图」，迁自旧 `Model/UiControllers.fs` 的 ShortcutRouter。拆成两层：本库只认平台无关的 ShortcutKey（按键名、ctrl 含 ⌘、shift、无修饰）；把 Avalonia 的 KeyEventArgs 映射成 ShortcutKey 留在宿主（`{ key = string e.Key; ctrl = Control||Meta; shift = Shift; noModifiers = KeyModifiers.None }`）。ShortcutAction 的 Escape 等是通用名，按根规则加了 `[<RequireQualifiedAccess>]`。 只依赖 BCL；命名空间沿用 `Wanxiang.UI`。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **所有按键与修饰键组合的解析结果与旧代码相同。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/Tests.fs` 通过：`ShortcutRouter resolveKey resolves basic key combinations correctly`（覆盖基础按键、ctrl、ctrl+shift、纯功能键与带修饰键的滚动类动作）。

# 迁入或覆盖的旧测试
- 无。CloseWindowTests、ContinueLinkUndoTests 里构造 KeyEventArgs 的用例测的是按键映射层，留在旧仓库。
