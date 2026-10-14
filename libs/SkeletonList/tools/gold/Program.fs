module Program

open System
open System.IO
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Media
open Avalonia.Themes.Fluent
open Wanxiang.Core
open Wanxiang.UI

type GenApp() =
    inherit Application()
    override this.Initialize() = this.Styles.Add(FluentTheme())

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

/// 旧 ChatView 显示骨架后取出滚动区内容（骨架面板）转写；减弱动效使透明度静态
[<EntryPoint>]
let main argv =
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let mutable text = ""
    session.Dispatch(Action(fun () ->
        MotionPolicy.setReduced true
        let msg: MessageActions = { copyText = ignore; regenerate = ignore; editAndFork = ignore; deleteMessage = ignore; downloadAttachment = ignore; openLink = ignore }
        let actions: ChatActions =
            { renameTitle = ignore; openSessionSettings = ignore; forkFromHere = ignore; stopGeneration = ignore; requestOlderHistory = ignore
              retryLast = ignore; toggleSidebar = ignore; focusHome = ignore; message = msg }
        let chat = ChatView(actions, fun _ -> Border() :> Control)
        chat.Build()
        chat.ShowSkeletonLoading()
        let layout = chat.Child :?> DockPanel
        let body = layout.Children.[1] :?> Grid
        let scroller = body.Children.[0] :?> ScrollViewer
        let panel = scroller.Content :?> Control
        scroller.Content <- null
        // 旧代码挂树前是静态透明度；新库同口径
        text <- Dump.tree FontFamily.Default panel), CancellationToken.None).GetAwaiter().GetResult()
    File.WriteAllText(argv[0], text)
    0
