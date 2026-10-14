module Program

// 用旧 ChatView 生成 ChatScreen 的整树金样：顶栏（含面包屑）+ 主体，按 Cases.names 的状态与窗口宽转写。

open System
open System.IO
open System.Text
open System.Threading
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Input
open Avalonia.Media
open Avalonia.LogicalTree
open Avalonia.Themes.Fluent
open Wanxiang.UI

type GenApp() =
    inherit Application()
    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        // 与旧 App.fs 一致：共享类选择器样式只装一次
        Wanxiang2.Theme.ThemeStyles(SharedTheme.manager).Install this.Styles

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

let private font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
let logo (size: float) = Border(Width = size, Height = size, Background = Brushes.Transparent) :> Control

let rec private find (pred: Control -> bool) (c: ILogical) : Control option =
    match c with
    | :? Control as cc when pred cc -> Some cc
    | _ -> c.LogicalChildren |> Seq.tryPick (find pred)

[<EntryPoint>]
let main argv =
    let out = argv[0]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let run (f: unit -> unit) = session.Dispatch(Action f, CancellationToken.None).GetAwaiter().GetResult()
    let card: Wanxiang2.MessageCardView.CardActions =
        { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore; DownloadAttachment = ignore; OpenLink = ignore; CopyLink = None; Recover = None }
    let make () =
        // 减弱动效：状态过渡瞬时落定，金样不随补间进度抖动
        SharedTheme.manager.Motion.SetReduced true
        let actions: ChatActions =
            { renameTitle = ignore; openSessionSettings = ignore; forkFromHere = ignore; stopGeneration = ignore; requestOlderHistory = ignore
              retryLast = ignore; toggleSidebar = ignore; focusHome = ignore; message = card }
        let c = ChatView(actions, logo)
        c.Build()
        c.SetStarterHandler ignore
        c
    let render (c: ChatView) streaming =
        c.HideEmpty()
        c.RenderMessages(Cases.messages, streaming, None, 14.5, false, None, Set.empty)
    let conversation (c: ChatView) =
        c.SetConversationChrome true
        c.SetTitle("京都行程", true)
        render c None
    let crumb available =
        Some { parentId = Cases.parent; parentAvailable = available; text = "分叉自「旅行」"; description = (if available then "回到父对话「旅行」" else "父对话已删除") }
    let show name (c: ChatView) =
        match name with
        | "empty" -> c.SetTitle("新对话", false); c.ShowEmpty(EmptyConversation, None)
        | "conversation" | "conversation-wide" -> conversation c
        | "rename" ->
            conversation c
            let title = (find (fun x -> AutomationProperties.GetName x = "重命名对话：京都行程") c).Value
            title.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.F2))
        | "generating" -> conversation c; c.SetGenerating(true, "")
        | "streaming" -> c.SetConversationChrome true; c.SetTitle("京都行程", true); c.SetGenerating(true, ""); render c (Some Cases.streaming)
        | "crumb-clickable" -> conversation c; c.SetBreadcrumb(crumb true, ignore)
        | "crumb-deleted" -> conversation c; c.SetBreadcrumb(crumb false, ignore)
        | "compact" -> conversation c; c.SetCompactMode true; c.SetGenerating(true, ""); c.SetBreadcrumb(crumb true, ignore)
        | "readonly-title" -> c.SetConversationChrome true; c.SetTitle("加载中…", false); render c None
        | _ -> failwith name
    for name, width in Cases.names do
        run (fun () ->
            let c = make ()
            show name c
            // 决策 350：顶栏模型芯片恒不可见，新库未迁移；金样里摘掉该死节点与它的列
            let chip = (find (fun x -> x.Name = "HeaderModelChip") c).Value
            let g = chip.Parent :?> Grid
            g.Children.Remove chip |> ignore
            g.ColumnDefinitions.RemoveAt 3
            sb.Append("== ").Append(name).Append('\n').Append((Dump.treeAt width 2000.0 font c).TrimEnd('\n')).Append('\n') |> ignore)
    Directory.CreateDirectory(Path.GetDirectoryName out) |> ignore
    File.WriteAllText(out, sb.ToString())
    printfn "wrote %s" out
    0
