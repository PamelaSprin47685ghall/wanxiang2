module Wanxiang2.MathFigure.Tests.Tests

open System
open System.IO
open System.Text.Json
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ThemeFonts
open Wanxiang2.MathFigure

let private golden = File.ReadAllText(Path.Combine(__SOURCE_DIRECTORY__, "golden", "figure.txt"))

/// 金样里每条公式的 HTML（旧生成器现场由 RichJint 产出）
let private htmls =
    golden.Split('\n')
    |> Array.filter (fun l -> l.StartsWith "html: ")
    |> Array.map (fun l -> JsonSerializer.Deserialize<string>(l.Substring 6) |> Option.ofObj)

let private look () =
    let ink = SolidColorBrush(Probe.ink.Color)
    ink, { Ink = ink :> IBrush }

/// 主题资源字典的替身：字体与展示式留白走资源键。
let private seedResources () =
    let r = Application.Current.Resources
    r["FontFamily.Body"] <- box (FontFamily ThemeFonts.body)
    r["Padding.MathBlock"] <- box (Thickness(0.0, 8.0))

let private run tex disp html (l: MathFigureMetrics) =
    Probe.probe (FontFamily ThemeFonts.body)
        (fun () -> MathFigures.tryInline l html 14.5 tex)
        (fun () -> MathFigures.tryBlock l html 14.5 tex)
        MathFigures.inlineContainer
        (fun c -> (c :?> MathFigure).Baseline)
        disp

[<Fact>]
let ``inline and display formulas lay out and paint exactly like the old MathVisual`` () =
    Headless.run (fun () ->
        seedResources ()
        let sb = Text.StringBuilder()
        Scenario.math
        |> List.iteri (fun i (tex, disp) ->
            let ink, l = look ()
            ink.Color <- Probe.ink.Color
            sb.Append("== ").Append(JsonSerializer.Serialize tex).Append(if disp then " display\n" else "\n") |> ignore
            sb.Append("html: ").Append(JsonSerializer.Serialize(Option.toObj htmls.[i])).Append('\n') |> ignore
            sb.Append(run tex disp htmls.[i] l) |> ignore)
        Assert.Equal(golden, sb.ToString()))

[<Fact>]
let ``no html means no figure so the caller falls back to source text`` () =
    Headless.run (fun () ->
        let _, l = look ()
        seedResources ()
        Assert.True((MathFigures.tryInline l None 14.5 "x").IsNone)
        Assert.True((MathFigures.tryBlock l (Some "<span>not katex</span>") 14.5 "x").IsNone))

[<Fact>]
let ``recoloring the live ink repaints without relayout`` () =
    Headless.run (fun () ->
        seedResources ()
        let ink, l = look ()
        let tex, _ = Scenario.math.[1]
        let before = run tex false htmls.[1] l
        ink.Color <- Colors.Red
        let after = run tex false htmls.[1] l
        let layout (s: string) = s.Substring(0, s.IndexOf " px=")
        Assert.Equal(layout before, layout after)
        Assert.NotEqual<string>(before, after))

[<Fact>]
let ``body font change relays out glyphs with the new family`` () =
    Headless.run (fun () ->
        seedResources ()
        let _, l = look ()
        let i = Scenario.math |> List.findIndex (fun (t, _) -> t.Contains "中文")
        let figure = new MathFigure(htmls.[i], false, 14.5, l)
        let win = Window(Content = figure)
        win.Show()
        Dispatcher.UIThread.RunJobs()
        let cjk () =
            figure.Picture.Value.Marks
            |> List.pick (function
                | MathMark.Glyph(t, _, tf, _) when t.Contains "中" -> Some tf.FontFamily.Name
                | _ -> None)
        Assert.Equal("Sarasa Gothic SC", cjk ())
        Application.Current.Resources["FontFamily.Body"] <- box (FontFamily ThemeFonts.mono)
        Dispatcher.UIThread.RunJobs()
        Assert.Equal("Sarasa Term SC", cjk ())
        win.Close())

[<Fact>]
let ``display padding follows the theme resource`` () =
    Headless.run (fun () ->
        seedResources ()
        let _, l = look ()
        let host = (MathFigures.tryBlock l htmls.[3] 14.5 "s").Value :?> Border
        let win = Window(Content = host)
        win.Show()
        Dispatcher.UIThread.RunJobs()
        Assert.Equal(8.0, host.Padding.Top)
        Application.Current.Resources["Padding.MathBlock"] <- box (Thickness(0.0, 12.0))
        Assert.Equal(12.0, host.Padding.Top)
        win.Close())

[<Fact>]
let ``inline container aligns to bottom and figures announce their source`` () =
    Headless.run (fun () ->
        seedResources ()
        let _, l = look ()
        let f = (MathFigures.tryInline l htmls.[0] 14.5 "x^2").Value
        Assert.Equal(BaselineAlignment.Bottom, (MathFigures.inlineContainer f).BaselineAlignment)
        Assert.Equal("公式：x^2", Avalonia.Automation.AutomationProperties.GetName f)
        Assert.Equal(box "x^2", ToolTip.GetTip f))

[<Fact>]
let ``katex glyph families come from the embedded fonts`` () =
    Headless.run (fun () ->
        let tf = Typeface(ThemeFonts.family "KaTeX_Main")
        let picture = MathDraw.tryLayout (FontFamily ThemeFonts.body) Brushes.Black 14.5 htmls.[9].Value |> Result.toOption |> Option.get
        let families =
            picture.Marks |> List.choose (function MathMark.Glyph(_, _, t, _) -> Some t.FontFamily.Name | _ -> None)
        Assert.Contains(tf.FontFamily.Name, families))
