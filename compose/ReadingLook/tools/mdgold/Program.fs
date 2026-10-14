module Program

open System
open System.IO
open System.Text
open System.Threading
open Avalonia
open Avalonia.Headless
open Avalonia.Themes.Fluent
open Wanxiang.UI

type GenApp() =
    inherit Application()
    override this.Initialize() = this.Styles.Add(FluentTheme())

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

/// 旧 IRichBackend：公式 HTML 查表，着色不提供（流式渲染本来就不着色）
type Table(map: Map<string * bool, string>) =
    interface IRichBackend with
        member _.HighlightHtml(_, _) = None
        member _.MathHtml(tex, disp) = Map.tryFind (tex, disp) map

[<EntryPoint>]
let main argv =
    let out, htmlTable = argv[0], argv[1]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let pass (label: string) =
        for name, raw in Samples.all do
            session.Dispatch(Action(fun () ->
                let view = MarkdownRenderer(14.5, ignore, ignore, false)
                sb.Append("== ").Append(label).Append(' ').Append(name).Append('\n').Append(Dump.tree (Media.FontFamily "avares://Wanxiang.UI/Assets/Fonts/#Sarasa Gothic SC") (view.RenderText raw)) |> ignore), CancellationToken.None).GetAwaiter().GetResult()
    // 先不装后端（公式退化成原文），再装上查表后端
    pass "plain"
    RichBackend.install (Table(MathHtml.table htmlTable))
    pass "rich"
    File.WriteAllText(out, sb.ToString())
    0
