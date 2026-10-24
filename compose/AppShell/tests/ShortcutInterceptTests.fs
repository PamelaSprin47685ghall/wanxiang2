module Wanxiang2.AppShell.Tests.ShortcutInterceptTests

// SPEC 7 回归：对话框 / 菜单打开时，全局快捷键必须被拦下。
//
// 真实语义（`compose/ShellCommands/ShellCommands.fs` 的 `HandleShortcut`，原文分支）：
//   | ShortcutAction.Escape ->            // Escape 在最前面，先给浮层退栈
//       if surface.OverlayEscape() then true
//       elif surface.IsSettingsOpen() then surface.CloseSettings(); true
//       elif surface.IsDrawerOpen() then surface.CloseDrawer(); true
//       elif surface.IsGenerating() then surface.StopGeneration(); true
//       else false
//   | ShortcutAction.NoShortcut -> false
//   | _ when surface.IsDialogOpen() || surface.IsPopupOpen() -> false   // 其余快捷键一律不接管、不执行
//   | ShortcutAction.ToggleTheme -> this.CycleTheme(); true
//   | ShortcutAction.OpenSettings -> surface.OpenSettings None; true
// 返回值即「是否接管」，宿主据此设 e.Handled；被拦的快捷键返回 false 且**不产生任何效果**。
//
// `IsDialogOpen` / `IsPopupOpen` 现在直接查真实控件层（`compose/AppShell/AppShell.fs`：
// `v.Overlay.Layer.IsDialogOpen` / `v.Overlay.Layer.IsPopupOpen`）。这里的判据也一律读同一条真实
// 状态，不往 `model.Overlay` 塞假值（那是已删除的死路径）。

open Avalonia.Controls
open Avalonia.Threading
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.AppShell.Tests

/// HandleShortcut 的真实入口：组合者公开属性 `Commands`。
let private sc (rig: Rig) (action: ShortcutAction) =
    rig.Shell.Commands.HandleShortcut action

/// 已派发的主题/偏好副作用次数（被拦时计数不能增长）。
let private countApplyTheme (rig: Rig) =
    rig.Fake.RecordedEffects
    |> List.sumBy (fun e ->
        match e with
        | Effect.ApplyTheme _ -> 1
        | _ -> 0)


let private countPrefsSave (rig: Rig) =
    rig.Fake.RecordedEffects
    |> List.sumBy (fun e ->
        match e with
        | Effect.PrefsSave _ -> 1
        | _ -> 0)


/// 连点主题直到回到起始偏好（主题轮转是三态环；测试结束后不留副作用）。
let private restoreTheme (rig: Rig) (start: ThemePreference) =
    let mutable guard = 0

    while rig.Shell.Model.Prefs.theme <> start && guard < 4 do
        sc rig ShortcutAction.ToggleTheme |> ignore
        Dispatcher.UIThread.RunJobs()
        guard <- guard + 1

    Assert.Equal(start, rig.Shell.Model.Prefs.theme)


[<Fact>]
let ``对话框打开时快捷键被拦，Escape 退栈后恢复可用`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        Dispatcher.UIThread.RunJobs()

        let theme0 = rig.Shell.Model.Prefs.theme
        let applyBefore = countApplyTheme rig
        let saveBefore = countPrefsSave rig

        // 真实控件入口打开确认框：DialogsDesk.Confirm → ConfirmPane → OverlayDesk.ShowDialog
        // → OverlayLayer 的对话框位（与删除对话/吊销设备那几条确认框同一条路）。
        let mutable confirmed = false

        rig.Views.Dialogs.Confirm("删除对话", "「一段对话」将被删除，宽限期内可撤销。", "删除", fun () -> confirmed <- true)
        |> ignore

        Dispatcher.UIThread.RunJobs()
        Assert.True(rig.Views.Overlay.Layer.IsDialogOpen, "确认框应挂在真实覆盖层的对话框位上")

        // 被拦：返回 false（不接管，宿主不设 e.Handled），且不产生任何效果。
        Assert.False(sc rig ShortcutAction.ToggleTheme)
        Assert.False(sc rig ShortcutAction.OpenSettings)
        Assert.False(sc rig ShortcutAction.NewConversation)
        Assert.False(sc rig ShortcutAction.FocusSearch)

        // 主题没轮转、偏好没保存、设置页没打开。
        Assert.Equal(theme0, rig.Shell.Model.Prefs.theme)
        Assert.Equal(applyBefore, countApplyTheme rig)
        Assert.Equal(saveBefore, countPrefsSave rig)
        Assert.False(rig.Shell.Model.Settings.Open)
        Assert.False(rig.Views.Settings.View.IsVisible)

        // Escape 不在拦截名单里：它先交给浮层退栈（`layer.HandleEscape` 的对话框分支），返回接管。
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()

        // 真实关闭：对话框位空了；Esc 走的是退栈，不是「确认删除」。
        Assert.False(rig.Views.Overlay.Layer.IsDialogOpen)
        Assert.False(confirmed)

        // 恢复可用：主题真的翻转（副作用派发出去、偏好进状态树）。
        Assert.True(sc rig ShortcutAction.ToggleTheme)
        Dispatcher.UIThread.RunJobs()
        // 主题落地的真实副作用是 PrefsSave（`SettingsUserMsg.PrefsChanged` 只产这一条；
        // ApplyTheme 由 prefs/系统偏好回读路径产，不在这条链上）。
        Assert.NotEqual(theme0, rig.Shell.Model.Prefs.theme)
        Assert.True(countPrefsSave rig > saveBefore)

        // 恢复可用：设置页真的打开（宿主整页槽位插入 + 状态树 Open）。
        Assert.True(sc rig ShortcutAction.OpenSettings)
        Dispatcher.UIThread.RunJobs()
        Assert.True(rig.Shell.Model.Settings.Open)
        Assert.True(rig.Views.Settings.View.IsVisible)

        // 收尾：设置页开着时 Escape 走的是「关设置」那条分支（浮层已空）。
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(rig.Shell.Model.Settings.Open)
        restoreTheme rig theme0)


[<Fact>]
let ``真实菜单（OverlayDesk.ShowMenu）打开时快捷键被拦，Escape 退栈后恢复可用`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        Dispatcher.UIThread.RunJobs()

        let theme0 = rig.Shell.Model.Prefs.theme
        let applyBefore = countApplyTheme rig
        let saveBefore = countPrefsSave rig
        let mutable copyClicked = false

        // 真实生产菜单入口：ChatScreen 的「···」按钮（header.MorePressed）走的就是
        // OverlayDesk.ShowMenu → PopupMenu.build → layer.ShowPopup，条目类型 PopupEntry。
        let anchor = Border(Width = 120.0, Height = 28.0)

        rig.Root.Children.Add anchor

        rig.Views.Overlay.ShowMenu(
            anchor,
            false,
            [
                Wanxiang2.PopupMenu.PopupEntry.create "复制消息" (fun () -> copyClicked <- true)
                Wanxiang2.PopupMenu.PopupEntry.create "删除消息" (fun () -> ())
            ]
        )

        Dispatcher.UIThread.RunJobs()
        Assert.True(rig.Views.Overlay.Layer.IsPopupOpen, "菜单卡片应在真实浮层位上")
        Assert.False(rig.Views.Overlay.Layer.IsDialogOpen)

        // 被拦：主题/设置/新建对话都不接管，也没有任何副作用。
        Assert.False(sc rig ShortcutAction.ToggleTheme)
        Assert.False(sc rig ShortcutAction.OpenSettings)
        Assert.False(sc rig ShortcutAction.NewConversation)
        Assert.Equal(theme0, rig.Shell.Model.Prefs.theme)
        Assert.Equal(applyBefore, countApplyTheme rig)
        Assert.Equal(saveBefore, countPrefsSave rig)
        Assert.False(rig.Shell.Model.Settings.Open)

        // Escape 放行：浮层先退栈（`layer.HandleEscape` 的 popup 分支优先于对话框）。
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(rig.Views.Overlay.Layer.IsPopupOpen)

        // 恢复可用：主题翻转，再轮转回起始值，不留副作用。
        Assert.True(sc rig ShortcutAction.ToggleTheme)
        Dispatcher.UIThread.RunJobs()
        Assert.NotEqual(theme0, rig.Shell.Model.Prefs.theme)
        Assert.True(countPrefsSave rig > saveBefore)

        // 设置页恢复可用，关掉后不留残留。
        Assert.True(sc rig ShortcutAction.OpenSettings)
        Dispatcher.UIThread.RunJobs()
        Assert.True(rig.Shell.Model.Settings.Open)
        Assert.True(rig.Views.Settings.View.IsVisible)
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(rig.Shell.Model.Settings.Open)

        restoreTheme rig theme0)


/// 原生 ContextMenu 挂在独立宿主（PopupRoot / OverlayPopupHost）上，不经过本层浮层
/// 卡片 —— 这是 IsPopupOpen 拦截的风险路径。Escape 交还原生具体类自带的关闭契约：
/// 实测边界：编程式 menu.Open(anchor) 打开的 ContextMenu，Escape 后 menu.IsOpen 仍为
/// true（具体类 Close 与基类 Close 两种调法都不翻转），故本用例只断「Escape 被原生
/// 处理器认领、没有外溢到设置/抽屉/生成」，不把「Escape 后仍开着」固化成通过条件。
/// 「关闭 → 可重开 → 快捷键恢复」这条完整契约由下一条原生 MenuFlyout 用例承担
/// （FlyoutBase.Hide 实测会翻转 IsOpen，账本同步）。
[<Fact>]
let ``原生 ContextMenu（独立弹层宿主）打开时快捷键被拦，Escape 交还原生关闭契约`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        Dispatcher.UIThread.RunJobs()

        let theme0 = rig.Shell.Model.Prefs.theme
        let applyBefore = countApplyTheme rig
        let saveBefore = countPrefsSave rig

        // 生产里的消息卡右键菜单就是原生 ContextMenu：Card.fs 把它挂在控件的
        // ContextMenu 属性上（`ContextMenu = this.Menu(...)`）。这里走同一接法。
        let anchor = Border(Width = 160.0, Height = 28.0)

        rig.Root.Children.Add anchor

        let menu = ContextMenu()

        menu.Items.Add(MenuItem(Header = "复制消息")) |> ignore
        menu.Items.Add(MenuItem(Header = "删除消息")) |> ignore
        anchor.ContextMenu <- menu
        menu.Open(anchor)
        Dispatcher.UIThread.RunJobs()

        Assert.True(menu.IsOpen, "原生 ContextMenu 应已打开")

        // 风险路径：原生菜单的独立宿主也必须被算成「浮层开着」。
        Assert.True(rig.Views.Overlay.Layer.IsPopupOpen, "原生 ContextMenu 的独立宿主应被识别为浮层")

        Assert.False(rig.Views.Overlay.Layer.IsDialogOpen)

        // 被拦：不切主题、不开设置，也没有任何副作用。
        Assert.False(sc rig ShortcutAction.ToggleTheme)
        Assert.False(sc rig ShortcutAction.OpenSettings)
        Assert.Equal(theme0, rig.Shell.Model.Prefs.theme)
        Assert.Equal(applyBefore, countApplyTheme rig)
        Assert.Equal(saveBefore, countPrefsSave rig)
        Assert.False(rig.Shell.Model.Settings.Open)

        // Escape 交给最上层：走 ContextMenu 自己的关闭契约，键盘不再漏给
        // 设置/抽屉/生成等其它壳层处理器（以下四项都没动 = 没有副作用外溢）。
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(theme0, rig.Shell.Model.Prefs.theme)
        Assert.Equal(applyBefore, countApplyTheme rig)
        Assert.Equal(saveBefore, countPrefsSave rig)
        Assert.False(rig.Shell.Model.Settings.Open)
        Assert.False(rig.Views.Settings.View.IsVisible))


[<Fact>]
let ``原生 MenuFlyout（独立弹层宿主）打开时快捷键被拦，Escape 关闭后恢复可用`` () =
    Headless.run (fun () ->
        let rig = Rig.create ()
        rig.Shell.Dispatch(WireRecv(AuthAccepted {| instanceId = "inst-test" |}))
        Dispatcher.UIThread.RunJobs()

        let theme0 = rig.Shell.Model.Prefs.theme
        let saveBefore = countPrefsSave rig

        // 生产里的原生 MenuFlyout：libs/PopupMenu/PopupMenu.fs 的原生下拉选择器用的
        // 就是 MenuFlyout；这里走 Avalonia 的 attached flyout 真实入口。
        let owner = Button(Content = "从此分叉")

        rig.Root.Children.Add owner

        let flyout = MenuFlyout()

        flyout.Items.Add(MenuItem(Header = "从此分叉")) |> ignore
        flyout.Items.Add(MenuItem(Header = "重命名")) |> ignore
        Avalonia.Controls.Primitives.FlyoutBase.SetAttachedFlyout(owner, flyout)
        Avalonia.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(owner)
        Dispatcher.UIThread.RunJobs()

        Assert.True(flyout.IsOpen, "原生 MenuFlyout 应已打开")
        Assert.True(rig.Views.Overlay.Layer.IsPopupOpen, "原生 MenuFlyout 的独立宿主应被识别为浮层")
        // 原生宿主不该被误判成对话框。
        Assert.False(rig.Views.Overlay.Layer.IsDialogOpen)

        Assert.False(sc rig ShortcutAction.ToggleTheme)
        Assert.False(sc rig ShortcutAction.OpenSettings)
        Assert.Equal(theme0, rig.Shell.Model.Prefs.theme)
        Assert.Equal(saveBefore, countPrefsSave rig)
        Assert.False(rig.Shell.Model.Settings.Open)

        // Escape 退栈：FlyoutBase.Hide（原生 flyout 自己的关闭契约，账本同步）。
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(flyout.IsOpen)
        Assert.False(rig.Views.Overlay.Layer.IsPopupOpen)

        // 关掉之后 flyout 仍可用。
        Avalonia.Controls.Primitives.FlyoutBase.ShowAttachedFlyout(owner)
        Dispatcher.UIThread.RunJobs()
        Assert.True(flyout.IsOpen, "原生 MenuFlyout 应能再次打开")
        Assert.True(rig.Views.Overlay.Layer.IsPopupOpen)
        Assert.False(sc rig ShortcutAction.ToggleTheme)
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(flyout.IsOpen)
        Assert.False(rig.Views.Overlay.Layer.IsPopupOpen)

        Assert.True(sc rig ShortcutAction.ToggleTheme)
        Dispatcher.UIThread.RunJobs()
        Assert.NotEqual(theme0, rig.Shell.Model.Prefs.theme)
        Assert.True(countPrefsSave rig > saveBefore)

        // 关闭后 OpenSettings 同样恢复可用（真的打开设置页），Escape 再收回去。
        Assert.True(sc rig ShortcutAction.OpenSettings)
        Dispatcher.UIThread.RunJobs()
        Assert.True(rig.Shell.Model.Settings.Open)
        Assert.True(rig.Views.Settings.View.IsVisible)
        Assert.True(sc rig ShortcutAction.Escape)
        Dispatcher.UIThread.RunJobs()
        Assert.False(rig.Shell.Model.Settings.Open)

        restoreTheme rig theme0)
