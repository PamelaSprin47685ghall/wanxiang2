# 2026-10-23 结构审计 IsDialogOpen 真实 Owner 归属与修复报告（静态 / 未运行）

- **报告性质**：结构审计核查、真实 Owner 修正与回归测试说明（未执行构建、测试或外部命令，测试尚未运行，不据此冒充动态凭据）
- **涉及文件**：`compose/AppShell/AppShell.fs`、`compose/AppShell/tests/ComposerTests.fs`

---

## 一、问题背景与调用后果核查

### 1.1 问题现状
在 `compose/AppShell/AppShell.fs:405` 原实现中：
```fsharp
IsDialogOpen = fun () ->
    match model.Overlay with
    | OverlayPhase.DialogOpen _ -> true
    | _ -> false
IsPopupOpen = fun () ->
    match model.Overlay with
    | OverlayPhase.PopupOpen _ -> true
    | _ -> false
```
状态树类型系统虽然定义了 `OverlayPhase.DialogOpen`，但在全仓架构中，该类型属于**无生产者分支**（见《展望.md》§4）：真实的对话框由 `DialogsDesk` 经 `overlay.ShowDialog(/* 其余处理 */)` 直接在 `OverlayLayer` 原生图层上开合，不向单一状态树发送消息。因此 `model.Overlay` 在正常运行态下恒为 `OverlayPhase.None`，导致 `surface.IsDialogOpen()` 恒为 `false`。

### 1.2 实际调用后果（快捷键穿透）
在 `compose/ShellCommands/ShellCommands.fs:126` 的快捷键分派核心中：
```fsharp
member this.HandleShortcut(shortcut: ShortcutAction) : bool =
    match shortcut with
    | ShortcutAction.Escape -> /* 其余处理 */
    | ShortcutAction.NoShortcut -> false
    | _ when surface.IsDialogOpen() || surface.IsPopupOpen() -> false
    | ShortcutAction.ToggleSidebar -> /* 其余处理 */
    | ShortcutAction.NewConversation -> /* 其余处理 */
```
该守卫的核心职责是：**在模态对话框或菜单弹出层打开时，拦截除 Escape 之外的所有全局快捷键**，避免用户在确认删除、输入配对码或选择选项时，因按下 `Ctrl+N`、`Ctrl+B`、`Ctrl+,` 或 `Ctrl+1..9` 而在后台意外触发新建对话、切换折叠或跳转会话等破坏性操作。

**此前后果**：由于 `IsDialogOpen()` 恒为 `false`，当模态对话框展示在用户眼前时，全局快捷键根本没有被拦截，导致键盘事件穿透并在底层工作区静默执行。

---

## 二、架构归属判定与修正方案

遵循 Common Law「事各有主」与《Kolmogorov 之书》「让真实所有者持有状态，不制造第二套真相与虚假生产者」的原则：
- 对话框是真实控件树上的浮动图层，真实物理状态持有者是 `views().Overlay.Layer.IsDialogOpen`；
- 弹出菜单的真实物理持有者是 `views().Overlay.Layer.IsPopupOpen`；
- **不必给状态树凭空制造无意义的消息与生产者**，直接让组合者查询真实持有者。

### 2.1 源码修正（`compose/AppShell/AppShell.fs`）
将 `ShellCommands` 所绑定的 `ShellSurface` 事实查询接回真实物理 Owner：
```fsharp
IsDialogOpen = fun () ->
    match views with
    | Some v -> v.Overlay.Layer.IsDialogOpen
    | None ->
        match model.Overlay with
        | OverlayPhase.DialogOpen _ -> true
        | _ -> false
IsPopupOpen = fun () ->
    match views with
    | Some v -> v.Overlay.Layer.IsPopupOpen
    | None ->
        match model.Overlay with
        | OverlayPhase.PopupOpen _ -> true
        | _ -> false
```
在视图未构建前安全回退到模型状态，视图就绪后以真实控件层可见性为准。

---

## 三、针对性回归测试

在 `compose/AppShell/tests/ComposerTests.fs` 增补确定性回归测试：
```fsharp
[<Fact>]
let ``对话框打开时模态拦截全局快捷键分派，关闭后恢复正常分派`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        authenticate rig

        // 初始无对话框：快捷键正常分派
        Assert.False rig.Views.Overlay.Layer.IsDialogOpen
        Assert.True(rig.Shell.Commands.HandleShortcut(ShortcutAction.ToggleSidebar))

        // 打开确认对话框：底层真实图层成为 IsDialogOpen
        rig.Views.Dialogs.ShowConfirm("确认对话框", "确认执行操作吗？", ignore)
        Dispatcher.UIThread.RunJobs()
        Assert.True rig.Views.Overlay.Layer.IsDialogOpen

        // 模态拦截：新建对话、切换侧栏、打开设置等全局快捷键必须被拦截（返回 false，不予执行）
        Assert.False(rig.Shell.Commands.HandleShortcut(ShortcutAction.NewConversation))
        Assert.False(rig.Shell.Commands.HandleShortcut(ShortcutAction.ToggleSidebar))
        Assert.False(rig.Shell.Commands.HandleShortcut(ShortcutAction.OpenSettings))

        // 关闭对话框：快捷键恢复正常分派
        rig.Views.Overlay.Layer.CloseDialog()
        Dispatcher.UIThread.RunJobs()
        Assert.False rig.Views.Overlay.Layer.IsDialogOpen
        Assert.True(rig.Shell.Commands.HandleShortcut(ShortcutAction.ToggleSidebar)))
```

---

## 四、可打红反例（Killable Mutation）

| 变异代码 | 变异位置 | 预期失败表现 |
|---|---|---|
| 将 `IsDialogOpen` 还原为只读 `model.Overlay` | `compose/AppShell/AppShell.fs` | 测试中 `Assert.False(rig.Shell.Commands.HandleShortcut(ShortcutAction.NewConversation))` 失败（实际返回了 `true`，模态拦截失效） |
| 误将 `IsDialogOpen` 实现为恒 `true` | `compose/AppShell/AppShell.fs` | 初始断言 `Assert.True(rig.Shell.Commands.HandleShortcut(ShortcutAction.ToggleSidebar))` 失败（无对话框时正常快捷键也被拦截） |
| 对话框关闭后未重置图层状态 | `libs/FocusOrigin/OverlayLayer.fs` | 收尾断言 `Assert.True(rig.Shell.Commands.HandleShortcut(ShortcutAction.ToggleSidebar))` 失败（关闭后快捷键未恢复） |

---

## 五、DevOps 执行命令指引

请 DevOps 调度执行组合者测试，确认该修复与回归用例运行结果：
```bash
./x compose
```
（或单独执行 AppShell 测试工程：`dotnet test compose/AppShell/tests/AppShell.Tests.fsproj -c Debug -p:RestorePackagesPath="$PWD/artifacts/packages"`）
