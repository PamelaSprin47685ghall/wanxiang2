module Wanxiang2.ComposerDesk.Tests.GoldenTests

// 整树金样：旧 Composer（tools/composergold 用 extract-ledger 533b85b 生成），按同一份 Dump 转写比对。

open System
open System.IO
open Avalonia.Media
open Xunit
open Wanxiang.UI
open Wanxiang2.Theme
open Wanxiang2.ComposerDesk
open Wanxiang2.ComposerDeskTests

let private golden name = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", name))

let private same (expected: string) (actual: string) =
    let e = expected.TrimEnd('\n').Split('\n')
    let a = actual.TrimEnd('\n').Split('\n')
    for i in 0 .. min e.Length a.Length - 1 do
        if e.[i] <> a.[i] then
            Assert.Fail(sprintf "line %d\nold: %s\nnew: %s\n(prev: %s)" (i + 1) e.[i] a.[i] (if i > 0 then e.[i - 1] else ""))
    Assert.Equal(e.Length, a.Length)

let private att (n: int) ready failed (mt: string) (name: string) =
    { attachmentId = Guid(n, 0s, 0s, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy, 0uy); sha256 = "x"; size = 2048L * int64 n; mediaType = mt; fileName = name; ready = ready; failed = failed }

[<Fact>]
let ``输入区各状态与旧 Composer 同一棵树`` () =
    Headless.run (fun () ->
        let font = FontFamily Wanxiang2.ThemeFonts.ThemeFonts.body
        let theme = new ThemeManager((fun () -> DateTimeOffset.UtcNow), false)
        let styles = ThemeStyles theme
        styles.Install Avalonia.Application.Current.Styles
        let actions =
            { Submit = (fun _ -> true); StopGeneration = ignore; PickAttachment = ignore; RemoveAttachment = ignore
              OpenModelPicker = ignore; DropFiles = ignore; PasteFromClipboard = (fun () -> false); OpenSessionSettings = ignore }
        let cases: (string * (ComposerDesk -> unit)) list =
            [ "idle", ignore
              "text", (fun c -> c.SetText "你好")
              "attachments", (fun c -> c.SetAttachments [ att 1 false false "image/png" "a.png"; att 2 true false "text/plain" "b.txt"; att 3 false true "text/plain" "c.txt" ])
              "generating", (fun c -> c.SetText "再来"; c.SetCanStop true; c.SetGenerating true)
              "disabled", (fun c -> c.SetEnabled(false, "连接已断开"))
              "vision-blocked", (fun c -> c.SetAttachments [ att 4 true false "image/png" "d.png" ]; c.SetModelVision(VisionNo, "X"))
              "compact", (fun c -> c.SetCompactMode true) ]
        let sb = Text.StringBuilder()
        for name, show in cases do
            let c = ComposerDesk(ComposerLooks.ofTheme theme styles, actions)
            c.SetEnabled(true, "")
            c.SetModelLabel "gpt-x"
            show c
            sb.Append("== ").Append(name).Append('\n').Append((Dump.tree font c.Host).TrimEnd('\n')).Append('\n') |> ignore
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "composer-new.txt"), sb.ToString())
        same (golden "composer.txt") (sb.ToString()))
