module Program
open System
open System.IO
open System.Text
open Avalonia
open Avalonia.Headless
open Avalonia.Media
open Wanxiang.UI

type GenApp() =
    inherit Application()

type Builder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<GenApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

let scales = [| 0.5; 0.6; 0.7; 0.8; 0.9; 1.0; 1.2; 1.44; 1.728; 2.074; 2.488 |]
let ratios = [| for a in scales do for b in scales -> a / b |] |> Array.distinct
let candidates =
    let b = 14.5 * 1.21
    [| yield b
       for r in ratios do
           yield b * r
           for q in ratios do yield b * r * q |]

// 旧 FormattedText 不暴露字号：用候选字号重量一遍，度量完全一致者即为当时的字号
let sizeOf (text: string) (tf: Typeface) (shaped: FormattedText) =
    candidates |> Array.tryFind (fun s ->
        let t = FormattedText(text, Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, s, Brushes.Black)
        t.WidthIncludingTrailingWhitespace = shaped.WidthIncludingTrailingWhitespace && t.Height = shaped.Height && t.Baseline = shaped.Baseline)

let f (v: float) = v.ToString("R", Globalization.CultureInfo.InvariantCulture)

// 金样：每段 KaTeX HTML 在 14.5 号字下排出的盒子与图元，以及每个字形的度量（供新实现回放）
let dump (sb: StringBuilder) (name: string) (html: string) =
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    line ("== " + name)
    line ("html: " + Text.Json.JsonSerializer.Serialize html)
    match MathLayout.tryRender Brushes.Black 14.5 html with
    | Error e -> line ("error: " + e)
    | Ok box ->
        line (sprintf "box: w=%s above=%s below=%s" (f box.width) (f box.above) (f box.below))
        for it in box.items do
            match it with
            | MathGlyph(text, shaped, tf, at) ->
                line (sprintf "glyph: %s family=%s size=%s x=%s y=%s | measure w=%s base=%s h=%s"
                        (Text.Json.JsonSerializer.Serialize text) tf.FontFamily.Name (match sizeOf text tf shaped with Some v -> f v | None -> "?") (f at.X) (f at.Y)
                        (f shaped.WidthIncludingTrailingWhitespace) (f shaped.Baseline) (f shaped.Height))
            | MathRule r -> line (sprintf "rule: x=%s y=%s w=%s h=%s" (f r.X) (f r.Y) (f r.Width) (f r.Height))
            | MathPath(_, m, clip) ->
                line (sprintf "path: m=%s,%s,%s,%s,%s,%s clip=%s,%s,%s,%s" (f m.M11) (f m.M12) (f m.M21) (f m.M22) (f m.M31) (f m.M32) (f clip.X) (f clip.Y) (f clip.Width) (f clip.Height))

[<EntryPoint>]
let main argv =
    let inputs = ResizeArray<string * string>()
    for p in Directory.GetFiles(argv[1], "*.html") |> Array.sort do
        inputs.Add(Path.GetFileNameWithoutExtension p, File.ReadAllText p)
    // 完整输入：直接调用旧 JintRichBackend 生成 KaTeX HTML（不再从按行存放的金样里截取）
    let backend = Wanxiang.App.JintRichBackend() :> IRichBackend
    for (tex, disp) in Scenario.math do
        match backend.MathHtml(tex, disp) with
        | Some h -> inputs.Add(sprintf "%s display=%b" (Text.Json.JsonSerializer.Serialize tex) disp, h)
        | None -> ()
    let sb = StringBuilder()
    use session = HeadlessUnitTestSession.StartNew(typeof<Builder>)
    session.Dispatch((fun () -> for (n, h) in inputs do dump sb n h), Threading.CancellationToken.None).Wait()
    File.WriteAllText(argv[0], sb.ToString())
    0
