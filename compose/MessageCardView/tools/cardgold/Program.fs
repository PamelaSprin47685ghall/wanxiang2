module Program

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

let private font = FontFamily "avares://Wanxiang.UI/Assets/Fonts/#Sarasa Gothic SC"

/// 品牌头像在两边都用同一个占位方块（真头像归 BrandMark，不在本金样范围）
let avatar () = Border(Width = 26.0, Height = 26.0, Background = Brushes.Transparent) :> Control

[<EntryPoint>]
let main argv =
    let out = argv[0]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let run (f: unit -> unit) = session.Dispatch(Action f, CancellationToken.None).GetAwaiter().GetResult()
    let actions: MessageActions =
        { copyText = ignore; regenerate = ignore; editAndFork = ignore; deleteMessage = ignore; downloadAttachment = ignore; openLink = ignore }
    MessageCard.setRecoverySink ignore
    for name, message, (f: Samples.Flags) in Samples.all do
        run (fun () ->
            let ctx: MessageContext =
                { fontSize = 14.5; autoCollapseReasoning = f.AutoCollapse; streaming = f.Streaming; isLastAssistant = f.IsLastAssistant
                  usage = f.Usage; missingAttachments = f.Missing; awaitingApproval = f.Awaiting; brandAvatar = avatar }
            sb.Append("== ").Append(name).Append('\n').Append(Dump.tree font (MessageCard.render message ctx actions f.Index)) |> ignore)
    for name, error, cont in Samples.errors do
        run (fun () ->
            let card = MessageCard.errorCardWith error ignore (Some ignore) (if cont then Some ignore else None)
            sb.Append("== ").Append(name).Append('\n').Append(Dump.tree font card) |> ignore)
    File.WriteAllText(out, sb.ToString())
    0
