# KeyChord

Avalonia 按键 → 与平台无关的按键形状（迁自旧 `Controls/KeyShortcut.fs` 的 `ofEvent`）。「键 → 意图」在 ShortcutKeys 库；`KeyChord` 与 `ShortcutKey` 的字段一一对应，换算与 `resolve` 由组合者做（本库不依赖兄弟库）。根规则见项目根目录 AGENTS.md。

## 外观

无外观，没有 Look 记录。

## 证据与旧测试

旧测试经 `KeyShortcut.resolve` 走整条快捷键路径（ConversationJourneyTests、UiStabilityTests 等），都要整个外壳，留旧仓库。本库用例逐个钉住换算规则（⌘=Ctrl、Shift、无修饰键），并在 headless 下用真实 KeyEventArgs 验证。
