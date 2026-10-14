module Wanxiang2.ChatScreen.Tests.GoldenTests

// 整树金样：旧 ChatView（顶栏 + 面包屑 + 主体，tools/screengold 用 extract-ledger 533b85b 生成），按同一份 Dump 与 Cases 转写比对。

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MessageCardView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.ChatScreen
open Wanxiang2.ChatScreenTests

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

let private same (expected: string) (actual: string) =
    let e = expected.TrimEnd('\n').Split('\n')
    let a = actual.TrimEnd('\n').Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

let private logo (size: float) = Border(Width = size, Height = size, Background = Brushes.Transparent) :> Control

let screen (theme: ThemeManager) (styles: ThemeStyles) =
    let overlay = OverlayDesk(Grid(), Desk.look ())
    let host =
        { Tint = CodeTint { HighlightHtml = (fun _ -> None); Background = (fun f -> f ()); After = (fun _ f -> f ()); Synchronous = true }
          MathHtml = (fun _ -> None)
          Wrap = Wanxiang2.MarkdownView.CodeWrapMemory false
          Clock = CaretClock()
          Touch = CardTouch()
          Now = fun () -> DateTimeOffset.UnixEpoch }
    let composer =
        ComposerDesk(
            ComposerLooks.ofTheme theme styles,
            { Submit = (fun _ -> true); StopGeneration = ignore; PickAttachment = ignore; RemoveAttachment = ignore
              OpenModelPicker = ignore; DropFiles = ignore; PasteFromClipboard = (fun () -> false); OpenSessionSettings = ignore })
    ChatScreen(
        ChatScreenLooks.ofTheme theme styles,
        MessageCards(CardLooks.ofTheme theme styles, host),
        composer, ApprovalDesk(theme, overlay, ignore), overlay, logo,
        { Card =
            { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore
              DownloadAttachment = ignore; OpenLink = ignore; CopyLink = None; Recover = None }
          RenameTitle = ignore; ToggleSidebar = ignore; OpenSessionSettings = ignore; ForkFromHere = ignore
          ExportConversation = ignore; StopGeneration = ignore; RequestOlderHistory = ignore; FocusHome = ignore
          OpenParent = ignore; SiblingSelected = ignore
          After = fun _ _ -> { new IDisposable with member _.Dispose() = () } })

let private frame streaming =
    { Messages = Cases.messages; Streaming = streaming; Error = None; FontSize = 14.5; AutoCollapseReasoning = false
      Usage = None; MissingAttachments = Set.empty }

[<Fact>]
let ``对话屏各状态与旧 ChatView 同一棵树`` () =
    Headless.run (fun () ->
        let font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        // 与生成器一致：减弱动效让状态过渡瞬时落定
        theme.Motion.SetReduced true
        let styles = ThemeStyles theme
        styles.Install Application.Current.Styles
        let sb = Text.StringBuilder()
        let render (s: ChatScreen) streaming =
            s.Pane.HideEmpty()
            s.Render(frame streaming)
        let conversation (s: ChatScreen) =
            s.SetConversationChrome true
            s.SetTitle("京都行程", true)
            render s None
        let crumb available =
            Some { parentId = Cases.parent; parentAvailable = available; text = "分叉自「旅行」"; description = (if available then "回到父对话「旅行」" else "父对话已删除") }
        for name, width in Cases.names do
            let s = screen theme styles
            s.Pane.SetStarterPick(Some ignore)
            match name with
            | "empty" -> s.SetTitle("新对话", false); s.Pane.ShowEmpty(ChatEmptyState.EmptyConversation, None)
            | "conversation" | "conversation-wide" -> conversation s
            | "rename" ->
                conversation s
                s.Header.TitleAction.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.F2))
            | "generating" -> conversation s; s.SetGenerating(true, "")
            | "streaming" -> s.SetConversationChrome true; s.SetTitle("京都行程", true); s.SetGenerating(true, ""); render s (Some Cases.streaming)
            | "crumb-clickable" -> conversation s; s.SetBreadcrumb(crumb true)
            | "crumb-deleted" -> conversation s; s.SetBreadcrumb(crumb false)
            | "compact" -> conversation s; s.SetCompactMode true; s.SetGenerating(true, ""); s.SetBreadcrumb(crumb true)
            | "readonly-title" -> s.SetConversationChrome true; s.SetTitle("加载中…", false); render s None
            | _ -> failwith name
            sb.Append("== ").Append(name).Append('\n').Append((Dump.treeAt width 2000.0 font s.Host).TrimEnd('\n')).Append('\n') |> ignore
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "screen-new.txt"), sb.ToString())
        same (golden "screen.txt") (sb.ToString()))
