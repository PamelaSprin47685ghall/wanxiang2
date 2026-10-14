module Program

// 用旧 Composer 生成 ComposerDesk 的整树金样（extract-ledger 533b85b），嵌入字体排版。

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

let att (n: int) ready failed (mt: string) (name: string) =
    { attachmentId = Guid(n, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy); sha256 = "x"; size = 2048L * int64 n; mediaType = mt; fileName = name; ready = ready; failed = failed }

[<EntryPoint>]
let main argv =
    let out = argv[0]
    let session = HeadlessUnitTestSession.StartNew(typeof<Builder>, AvaloniaTestIsolationLevel.PerTest)
    let sb = StringBuilder()
    let run (f: unit -> unit) = session.Dispatch(Action f, CancellationToken.None).GetAwaiter().GetResult()
    let make () =
        let actions: ComposerActions =
            { submit = (fun _ -> true); stopGeneration = ignore; pickAttachment = ignore; removeAttachment = ignore
              openModelPicker = ignore; dropFiles = ignore; pasteFromClipboard = (fun () -> false) }
        let c = Composer(actions)
        c.Build()
        c.SetEnabled(true, "")
        c.SetModelLabel "gpt-x"
        c
    let cases: (string * (Composer -> unit)) list =
        [ "idle", ignore
          "text", (fun c -> c.SetText "你好")
          "attachments", (fun c -> c.SetAttachments [ att 1 false false "image/png" "a.png"; att 2 true false "text/plain" "b.txt"; att 3 false true "text/plain" "c.txt" ])
          "generating", (fun c -> c.SetText "再来"; c.SetCanStop true; c.SetGenerating true)
          "disabled", (fun c -> c.SetEnabled(false, "连接已断开"))
          "vision-blocked", (fun c -> c.SetAttachments [ att 4 true false "image/png" "d.png" ]; c.SetModelVision(VisionNo, "X"))
          "compact", (fun c -> c.SetCompactMode true) ]
    for name, show in cases do
        run (fun () ->
            let c = make ()
            show c
            sb.Append("== ").Append(name).Append('\n').Append((Dump.tree font c).TrimEnd('\n')).Append('\n') |> ignore)
    Directory.CreateDirectory(Path.GetDirectoryName out) |> ignore
    File.WriteAllText(out, sb.ToString())
    printfn "wrote %s" out
    0
