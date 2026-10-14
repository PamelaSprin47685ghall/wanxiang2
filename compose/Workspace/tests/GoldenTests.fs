module Wanxiang2.Workspace.Tests.GoldenTests

// 整树金样：旧 AppShell.Build 布局段（tools/workspacegold 用 extract-ledger 533b85b 生成），新 Workspace 按同一份 Cases 转写比对。
// 侧栏与对话屏只比根节点一行（各有自己的金样）；输入区连同审批附属位整棵比。

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.CodeTint
open Wanxiang2.MessageCardView
open Wanxiang2.ComposerDesk
open Wanxiang2.ApprovalDesk
open Wanxiang2.OverlayDesk
open Wanxiang2.SidebarDesk
open Wanxiang2.ChatScreen
open Wanxiang2.Workspace
open Wanxiang2.WorkspaceTests

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

let private same (expected: string) (actual: string) =
    let e = expected.TrimEnd('\n').Split('\n')
    let a = actual.TrimEnd('\n').Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

let private rig (theme: ThemeManager) (styles: ThemeStyles) (collapsed: bool) =
    let root = Grid()
    let overlay = OverlayDesk(root, Desk.look ())
    let logo (s: float) = Border(Width = s, Height = s) :> Control
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
    let approvals = ApprovalDesk(theme, overlay, ignore)
    let ws: Workspace option ref = ref None
    let chat =
        ChatScreen(
            ChatScreenLooks.ofTheme theme styles,
            MessageCards(CardLooks.ofTheme theme styles, host),
            composer, approvals, overlay, logo,
            { Card =
                { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore
                  DownloadAttachment = ignore; OpenLink = ignore; CopyLink = None; Recover = None }
              RenameTitle = ignore; ToggleSidebar = (fun () -> ws.Value |> Option.iter (fun w -> w.ToggleSidebar() |> ignore))
              OpenSessionSettings = ignore; ForkFromHere = ignore; ExportConversation = ignore; StopGeneration = ignore
              RequestOlderHistory = ignore; FocusHome = ignore; OpenParent = ignore; SiblingSelected = ignore
              After = fun _ _ -> { new IDisposable with member _.Dispose() = () } })
    let sidebar =
        SidebarDesk(
            theme, overlay,
            { newConversation = ignore; openConversation = ignore; renameConversation = ignore; deleteConversation = ignore
              setPinned = (fun _ _ -> ()); setArchived = (fun _ _ -> ()); selectionAllPinned = (fun _ -> false)
              setPinnedMany = (fun _ _ -> ()); setArchivedMany = (fun _ _ -> ()); deleteMany = ignore
              duplicateAsFork = ignore; exportConversation = ignore; openSettings = ignore; reconnect = ignore
              toggleArchivedVisibility = ignore; closeNavigation = ignore },
            logo)
    let workspace =
        Workspace(theme, root, sidebar, chat, composer,
                  { PrefersTouch = (fun () -> true); SidebarWidth = (fun () -> 280.0); InitialSidebarCollapsed = collapsed })
    ws.Value <- Some workspace
    // AppShell 空闲面：输入区可用（IdleSurface.composerEnabled）
    composer.SetEnabled(true, "")
    root, workspace, chat, sidebar, approvals

[<Fact>]
let ``工作区各状态与旧 AppShell 布局段同一棵树`` () =
    Headless.run (fun () ->
        let font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        // 与生成器一致：减弱动效让状态过渡瞬时落定
        theme.Motion.SetReduced true
        let styles = ThemeStyles theme
        styles.Install Application.Current.Styles
        let sb = Text.StringBuilder()
        for name, width in Cases.names do
            let root, ws, chat, sidebar, approvals = rig theme styles false
            approvals.SetConversation(Some Cases.conversation)
            ws.ObserveViewport width
            match name with
            | "wide" | "compact" -> ()
            | "collapsed" -> ws.ToggleSidebar() |> ignore
            | "starting" -> ws.SetStarting true |> ignore
            | "approval" | "compact-approval" -> approvals.Handle Cases.approval |> ignore
            | "drawer" -> ws.SetCompactNavigation true |> ignore
            | _ -> failwith name
            root.Children.Remove ws.Host |> ignore
            Dump.cut <- fun c -> obj.ReferenceEquals(c, sidebar.View) || obj.ReferenceEquals(c, chat.Host)
            sb.Append("== ").Append(name).Append('\n').Append((Dump.treeAt width 900.0 font ws.Host).TrimEnd('\n')).Append('\n') |> ignore
        Dump.cut <- fun _ -> false
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "workspace-new.txt"), sb.ToString())
        same (golden "workspace.txt") (sb.ToString()))
