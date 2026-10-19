module Wanxiang2.ChatScreen.Tests.PerfTests

// 迁自旧 IncrementalRenderTests「长对话的流式刷新代价与消息条数无关」：200 条历史下流式每帧 < 20ms。
// 卡片身份直接用 ScreenKey 结构相等对账；每帧格式化身份（%A）会把这条线压穿。

open System
open System.Diagnostics
open Avalonia
open Avalonia.Controls
open Xunit
open Wanxiang.UI
open Wanxiang.Core
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MessageCardView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.ChatScreen
open Wanxiang2.ChatScreenTests

let private messageAt (index: int) : MessageView =
    { MessageView.empty with
        role = (if index % 2 = 0 then "user" else "assistant")
        text = $"一段示例正文，带 **加粗** 与 `代码`。\n\n第 {index} 条"
        commitId = Some(uint64 index)
        committedAt = Some DateTimeOffset.UtcNow }

let private createScreen (theme: ThemeManager) (styles: ThemeStyles) =
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let host =
        { Tint = CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }
          MathHtml = (fun _ -> None)
          Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
          Clock = CaretClock()
          Touch = CardTouch()
          Now = fun () -> DateTimeOffset.UnixEpoch }
    let cards = MessageCards(CardLooks.ofTheme theme styles, host)
    let composer =
        ComposerDesk(
            ComposerMetrics.ofTheme theme styles,
            { Submit = (fun _ -> true); StopGeneration = ignore; PickAttachment = ignore; RemoveAttachment = ignore
              OpenModelPicker = ignore; DropFiles = ignore; PasteFromClipboard = (fun () -> false); OpenSessionSettings = ignore })
    let approvals = ApprovalDesk(theme, overlay, ignore)
    let cardActions =
        { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore; DownloadAttachment = ignore
          OpenLink = ignore; CopyLink = None; Recover = None }
    let actions =
        { Card = cardActions
          RenameTitle = ignore; ToggleSidebar = ignore; OpenSessionSettings = ignore; ForkFromHere = ignore
          ExportConversation = ignore; StopGeneration = ignore; RequestOlderHistory = ignore; FocusHome = ignore
          OpenParent = ignore; SiblingSelected = (fun _ -> ()); After = (fun _ _ -> { new IDisposable with member _.Dispose() = () }) }
    ChatScreen(ChatScreenMetrics.ofTheme theme styles, cards, composer, approvals, overlay, (fun _ -> Border() :> Control), actions)

[<Fact>]
let ``长对话的流式刷新代价与消息条数无关`` () =
    Headless.run (fun () ->
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles theme
        styles.Install Application.Current.Styles
        let s = createScreen theme styles
        let w = Window(Width = 900.0, Height = 800.0, Content = s.Host)
        w.Show()
        s.SetConversationChrome true
        s.Pane.HideEmpty()
        let messages = [ for i in 1 .. 200 -> messageAt i ]
        let frame n =
            { Messages = messages; Streaming = Some { MessageView.empty with role = "assistant"; text = String('x', n) }
              Error = None; FontSize = 14.5; AutoCollapseReasoning = false; ReducedMotion = false; Usage = None; MissingAttachments = Set.empty }
        s.Render(frame 1)
        let watch = Stopwatch.StartNew()
        for n in 2 .. 21 do s.Render(frame n)
        watch.Stop()
        let perFrame = watch.Elapsed.TotalMilliseconds / 20.0
        w.Close()
        Assert.True(perFrame < 20.0, $"200 条历史下流式每帧 {perFrame:F2} ms，仍然太贵"))
