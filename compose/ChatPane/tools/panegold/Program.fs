module Program

// 用旧 ChatView 生成 ChatPane 的整树金样：整张 ChatView 按 Dump.tree 排版转写，再截出主体（DockPanel 第二个孩子，
// 即滚动区、空态滚动区、「回到最新」与更早一页失败横幅）。顶栏与面包屑不在 ChatPane 范围。

open System
open System.IO
open System.Text
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Headless
open Avalonia.Media
open Avalonia.Themes.Fluent
open Wanxiang.UI

type GenApp() =
    inherit Application()
    override this.Initialize() = this.Styles.Add(FluentTheme())

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

let private font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body

/// 品牌标两边用同一个占位方块
let logo (size: float) = Border(Width = size, Height = size, Background = Brushes.Transparent) :> Control

/// 截出主体子树：根是 ChatView，第 1 层 DockPanel，第 2 层 [顶栏 Border; 主体 Grid]
let body (dump: string) =
    let lines = dump.Split('\n')
    let start = lines |> Array.findIndex (fun l -> l.StartsWith "    Grid")
    // 主体是最后一个子树：取到结尾。多行提示文字的续行不带缩进，原样保留
    lines.[start..]
    |> fun a -> (if a.Length > 0 && a.[a.Length - 1] = "" then a.[.. a.Length - 2] else a)
    |> Array.map (fun l -> if l.StartsWith "    " then l.Substring 4 else l)
    // 根在旧 ChatView 里是 DockPanel 的孩子：去掉只在那里才有的停靠属性
    |> Array.mapi (fun i l -> if i = 0 then l.Replace(" dock=Left", "") else l)
    |> String.concat "\n"

[<EntryPoint>]
let main argv =
    let out = argv[0]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let run (f: unit -> unit) = session.Dispatch(Action f, CancellationToken.None).GetAwaiter().GetResult()
    let card: Wanxiang2.MessageCardView.CardActions =
        { CopyText = ignore; Regenerate = ignore; EditAndFork = ignore; DeleteMessage = ignore; DownloadAttachment = ignore; OpenLink = ignore; CopyLink = None; Recover = None }
    let make () =
        let actions: ChatActions =
            { renameTitle = ignore; openSessionSettings = ignore; forkFromHere = ignore; stopGeneration = ignore; requestOlderHistory = ignore
              retryLast = ignore; toggleSidebar = ignore; focusHome = ignore; message = card }
        let c = ChatView(actions, logo)
        c.Build()
        c.SetStarterHandler ignore
        c.SetPresetStarters [ "晨记", "每天早上写三件", ignore ]
        c
    let cases: (string * (ChatView -> unit)) list =
        [ "messages", (fun c -> c.HideEmpty())
          "skeleton", (fun c -> c.HideEmpty(); c.ShowSkeletonLoading())
          "older-failure", (fun c -> c.HideEmpty(); c.ShowOlderPageFailure("没能取到更早的消息：超时", ignore))
          "empty-conversation", (fun c -> c.ShowEmpty(EmptyConversation, None))
          "no-conversation-primary", (fun c -> c.ShowEmpty(NoConversation, Some("新对话", ignore)))
          "not-connected", (fun c -> c.ShowEmpty(NotConnected, Some("连接主机", ignore)))
          "connection-failed", (fun c -> c.ShowEmpty(ConnectionFailed "超时", Some("重试", ignore)))
          "no-provider", (fun c -> c.ShowEmpty(NoProvider, Some("添加模型", ignore))) ]
    for name, show in cases do
        run (fun () ->
            let c = make ()
            show c
            sb.Append("== ").Append(name).Append('\n').Append(body (Dump.tree font c)).Append('\n') |> ignore)
    Directory.CreateDirectory(Path.GetDirectoryName out) |> ignore
    File.WriteAllText(out, sb.ToString())
    printfn "wrote %s" out
    0
