# EscapeChain

全局快捷键在壳层的落点，迁自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `HandleShortcut`：`EscapeChain.decide`（浮层 → 设置页 → 窄屏抽屉 → 停止生成，D3/D4）、`blockedByOverlay`（对话框或弹出菜单打开时其余快捷键不接管）、`conversationAt`（Ctrl+1..9 按侧栏可见顺序，C2）。浮层自己接 Escape（`overlay.HandleEscape()`）仍由宿主先调用，结果作为第一个参数。只依赖 BCL；命名空间沿用 `Wanxiang.UI`。`EscapeTarget` 分支名较泛，加 RequireQualifiedAccess。根规则见项目根目录 AGENTS.md。

# LEMMA
1. **落点与旧代码一致。** [根 AXIOM: 1]

# SPEC
1. [LEMMA: 1] `tests/golden/expected.txt` 由 `tools/OldEscapeChain.fs`（旧分支原样摘录）跑 `tests/Render.fs` 生成，覆盖 16 种 Escape 组合；金标准用例通过。
2. `tools/gen-golden.fsx` 重跑后 `tests/golden` 逐字节不变。

# 迁入或覆盖的旧测试
- 旧整壳 UI 测试经过这些分支但同时依赖 Avalonia 焦点，留在旧仓库；新增 `D3/D4 抽屉开着时 Escape 先收抽屉，不停止生成`。
