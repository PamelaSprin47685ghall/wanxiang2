module Program

open System
open System.IO
open System.Text
open System.Threading
open Avalonia
open Avalonia.Headless
open Wanxiang.UI

type GenApp() =
    inherit Application()

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

/// 旧 IRichBackend 的适配：KaTeX HTML 取自 RichJint 包（已有自己的金样）
type Adapter(jint: Wanxiang.App.JintRichBackend) =
    interface IRichBackend with
        member _.HighlightHtml(code, lang) = jint.HighlightHtml(code, lang)
        member _.MathHtml(tex, disp) = jint.MathHtml(tex, disp)

[<EntryPoint>]
let main argv =
    let jint = Wanxiang.App.JintRichBackend()
    RichBackend.install (Adapter jint)
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    for (tex, disp) in Scenario.math do
        session.Dispatch(Action(fun () ->
            sb.Append("== ").Append(Text.Json.JsonSerializer.Serialize tex).Append(if disp then " display\n" else "\n") |> ignore
            let html = jint.MathHtml(tex, disp)
            sb.Append("html: ").Append(Text.Json.JsonSerializer.Serialize html).Append('\n') |> ignore
            sb.Append(
                Probe.probe Tokens.fontFamily
                    (fun () -> MathRender.tryInline 14.5 (14.5 * 1.62) tex)
                    (fun () -> MathRender.tryBlock 14.5 tex)
                    MathRender.inlineContainer
                    (fun c -> (c :?> MathVisual).Baseline)
                    disp) |> ignore), CancellationToken.None).GetAwaiter().GetResult()
    File.WriteAllText(argv[0], sb.ToString())
    0
