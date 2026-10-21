module Wanxiang2.ComposerDesk.Tests.PromptHistoryRecallTests

// 发送历史召回的回归测试（2026-10-21 W7-CLEANBREAK 恢复）。
//
// 这段能力曾在提交 `82d5628` 里随 `compose/EntryShells/Composer.fs` 一起被删，
// 功能静默消失；此处按旧实现的行为恢复，并钉住它的真实闭环：
//   被接受的发送 → 输入框清空 → 写半截草稿 → Up 召回 → Down 退回草稿 / Esc 退回草稿。
// 边界：选区存在时不召回（不吞掉选区）；光标不在行首/末尾时不误召回。

open System
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ComposerDesk
open Wanxiang2.ComposerDeskTests
open Wanxiang2.WorkspaceState

let private rig () =
    let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
    let styles = ThemeStyles(theme)
    styles.Install Avalonia.Application.Current.Styles
    let sent = ResizeArray<string>()
    let actions =
        { Submit = (fun t -> sent.Add t; true)
          TextChanged = (fun _ -> ())
          CaretChanged = (fun _ -> ())
          StopGeneration = ignore
          PickAttachment = ignore
          RemoveAttachment = ignore
          OpenModelPicker = ignore
          DropFiles = ignore
          PasteFromClipboard = (fun () -> false)
          OpenSessionSettings = ignore }
    let desk = ComposerDesk(actions)
    let w = Window(Width = 800.0, Height = 300.0, Content = desk.Host)
    w.Resources.MergedDictionaries.Add(theme.Resources :> Avalonia.Controls.IResourceDictionary)
    w.Show()
    Dispatcher.UIThread.RunJobs()
    desk, sent, w

let private emptyComposerVS () =
    (Workspace.project (Workspace.init ())).Composer

/// 造一个按键事件（与 compose/AppShell、compose/ChatScreen 的测试同一口径：
/// `KeyEventArgs(RoutedEvent = …, Key = …, KeyModifiers = …)`）。
let private keyEvent (key: Key) (mods: KeyModifiers) : KeyEventArgs =
    KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = mods)

/// 按一次键：走控件真实的按键管线（ComposerBox 的 KeyDown 处理器 → 组合者的 keyHook）。
let private press (input: TextBox) (key: Key) (mods: KeyModifiers) =
    let e = keyEvent key mods
    e.Source <- input
    input.RaiseEvent(e)
    Dispatcher.UIThread.RunJobs()
    e.Handled

// 被接受的发送进历史：输入框清空后，写入半截草稿，Up 召回上一条已发送。
[<Fact>]
let ``已接受的发送进历史，Up 召回，Down 退回草稿`` () =
    Headless.run (fun () ->
        let desk, sent, w = rig ()
        let input = desk.Box.Input
        // 一次被接受的发送
        desk.Render { emptyComposerVS () with Enabled = true; Text = "第一条消息" }
        desk.Submit()
        Assert.Equal<string list>([ "第一条消息" ], List.ofSeq sent)
        // 发送后输入框清空
        Assert.Equal("", desk.Text)
        // 写半截草稿，光标放在行首（PromptHistory 的规则：单行文本只在
        // 「输入框为空」或「光标在行首」时召回；草稿是按下 Up 那一刻输入框里的文本）。
        input.Text <- "半截草稿"
        input.CaretIndex <- 0
        // Up：召回上一条已发送
        let handled = press input Key.Up KeyModifiers.None
        Assert.True(handled)
        Assert.Equal("第一条消息", desk.Text)
        // Down：翻过最新一条，回到半截草稿
        press input Key.Down KeyModifiers.None |> ignore
        Assert.Equal("半截草稿", desk.Text)
        w.Close())

// Esc：召回中退回半截草稿。
[<Fact>]
let ``召回中 Esc 退回半截草稿`` () =
    Headless.run (fun () ->
        let desk, _sent, w = rig ()
        let input = desk.Box.Input
        desk.Render { emptyComposerVS () with Enabled = true; Text = "第一条消息" }
        desk.Submit()
        input.Text <- "半截草稿"
        input.CaretIndex <- 0
        press input Key.Up KeyModifiers.None |> ignore
        Assert.Equal("第一条消息", desk.Text)
        press input Key.Escape KeyModifiers.None |> ignore
        Assert.Equal("半截草稿", desk.Text)
        w.Close())

// 未被接受的发送不进历史：门控没过（没有文本）时提交，Up 什么都不该召回。
[<Fact>]
let ``未被接受的发送不进历史`` () =
    Headless.run (fun () ->
        let desk, sent, w = rig ()
        let input = desk.Box.Input
        // 空文本：门控挡住，宿主没接管
        desk.Render { emptyComposerVS () with Enabled = true; Text = "" }
        desk.Submit()
        Assert.Equal<string list>([], List.ofSeq sent)
        // Up 无历史可召回：输入框内容不变
        input.Text <- "还在"
        input.CaretIndex <- input.Text.Length
        press input Key.Up KeyModifiers.None |> ignore
        Assert.Equal("还在", desk.Text)
        w.Close())

// 有选区时不召回：选区优先，Up 不该吞掉用户的选区操作。
[<Fact>]
let ``有选区时不召回`` () =
    Headless.run (fun () ->
        let desk, _sent, w = rig ()
        let input = desk.Box.Input
        desk.Render { emptyComposerVS () with Enabled = true; Text = "第一条消息" }
        desk.Submit()
        input.Text <- "半截草稿"
        input.SelectionStart <- 0
        input.SelectionEnd <- 3
        let caret = input.CaretIndex
        press input Key.Up KeyModifiers.None |> ignore
        // 内容没被召回改写，选区还在
        Assert.Equal("半截草稿", desk.Text)
        Assert.Equal(caret, input.CaretIndex)
        w.Close())

// 光标不在行首时不误召回：多行文本、光标在中间，Up 不该把内容换掉。
[<Fact>]
let ``光标不在行首时不误召回`` () =
    Headless.run (fun () ->
        let desk, _sent, w = rig ()
        let input = desk.Box.Input
        desk.Render { emptyComposerVS () with Enabled = true; Text = "第一条消息" }
        desk.Submit()
        input.Text <- "第一行\n第二行"
        input.CaretIndex <- 4
        press input Key.Up KeyModifiers.None |> ignore
        Assert.Equal("第一行\n第二行", desk.Text)
        w.Close())

// 外部改写输入框（草稿回填）会退出召回态。
[<Fact>]
let ``草稿回填退出召回态`` () =
    Headless.run (fun () ->
        let desk, _sent, w = rig ()
        let input = desk.Box.Input
        desk.Render { emptyComposerVS () with Enabled = true; Text = "第一条消息" }
        desk.Submit()
        input.Text <- "半截草稿"
        input.CaretIndex <- 0
        press input Key.Up KeyModifiers.None |> ignore
        Assert.Equal("第一条消息", desk.Text)
        // 外部回填：退出召回，Up 回到「没有历史」的行为
        desk.ApplyDraftText "回填的草稿"
        Assert.Equal("回填的草稿", desk.Text)
        // 退出召回后，Up 不再拿回旧条目（草稿被回填内容替换了）
        press input Key.Up KeyModifiers.None |> ignore
        Assert.Equal("回填的草稿", desk.Text)
        w.Close())

// 打字时不被回写打断（2026-10-21 W7-CLEANBREAK）。
//
// 文本与光标现在会进状态树，状态树再把投影渲回输入区——
// 反馈环的危险就在这里：**回写会把光标拉回末尾、会把输入法组合打断**。
// 输入区只在「文本不同」时回写（`libs/AttachmentTray/ComposerBox.fs` 的 `if input.Text <> vs.Text`），
// 所以文本相同时不会回写；光标则靠 `CaretChanged` 保持同步。
//
// 这条用例断的是**接线**：打字到中间 → 光标回调 → 状态树 → 投影 → 再渲回去，光标不动。

/// 打字到中间：光标回调把位置送进状态树，投影带着同一个光标回来，输入区不回写、光标不动。
[<Fact>]
let ``打字到中间光标不被回写拉回末尾`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles(theme)
        styles.Install Avalonia.Application.Current.Styles
        let caretSeen = ResizeArray<int>()
        let actions =
            { Submit = (fun _ -> true)
              TextChanged = (fun _ -> ())
              CaretChanged = (fun c -> caretSeen.Add c)
              StopGeneration = ignore
              PickAttachment = ignore
              RemoveAttachment = ignore
              OpenModelPicker = ignore
              DropFiles = ignore
              PasteFromClipboard = (fun () -> false)
              OpenSessionSettings = ignore }
        let desk = ComposerDesk(actions)
        let w = Window(Width = 800.0, Height = 300.0, Content = desk.Host)
        w.Resources.MergedDictionaries.Add(theme.Resources :> Avalonia.Controls.IResourceDictionary)
        w.Show()
        Dispatcher.UIThread.RunJobs()
        let input = desk.Box.Input
        // 打到中间
        input.Text <- "abcdef"
        input.CaretIndex <- 3
        Dispatcher.UIThread.RunJobs()
        // 光标回调确实收到了 3
        Assert.Contains(3, caretSeen)
        // 位置没被拉回末尾
        Assert.Equal(3, input.CaretIndex)
        w.Close())

/// 打字 → 组合者 → 宿主的「文本变了」回调（2026-10-21 W7-CLEANBREAK 修）。
///
/// 此前 `ComposerDeskActions` 里**没有**「文本变了」这个出口：控件库的 `TextChanged`
/// 只用来重算发送门控，文本从不进状态树——于是草稿永远存不进本机、
/// 发送门控读到的也是空文本。
[<Fact>]
let ``打字时组合者把文本接出去`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles(theme)
        styles.Install Avalonia.Application.Current.Styles
        let seen = ResizeArray<string>()
        let actions =
            { Submit = (fun _ -> true)
              TextChanged = (fun t -> seen.Add t)
              CaretChanged = (fun _ -> ())
              StopGeneration = ignore
              PickAttachment = ignore
              RemoveAttachment = ignore
              OpenModelPicker = ignore
              DropFiles = ignore
              PasteFromClipboard = (fun () -> false)
              OpenSessionSettings = ignore }
        let desk = ComposerDesk(actions)
        let w = Window(Width = 800.0, Height = 300.0, Content = desk.Host)
        w.Resources.MergedDictionaries.Add(theme.Resources :> Avalonia.Controls.IResourceDictionary)
        w.Show()
        Dispatcher.UIThread.RunJobs()
        // 模拟用户打字：给输入框设值（Avalonia 会触发 TextChanged）
        desk.Box.Input.Text <- "你好"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "你好" ], List.ofSeq seen)
        w.Close())
