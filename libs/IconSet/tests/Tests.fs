module Wanxiang2.IconSet.Tests.Tests

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Xunit
open Wanxiang2.IconSet

/// 金样由照抄的旧 Icons.fs 的语义映射生成
[<Fact>]
let ``golden: every glyph maps to the old Lucide kind`` () =
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "iconset.txt"))
    let actual = IconSet.all |> List.map (fun g -> sprintf "%A %A\n" g (IconSet.kind g)) |> String.concat ""
    Assert.Equal(expected, actual)

[<Fact>]
let ``the icon keeps the live brush and follows the live size`` () =
    Headless.run (fun () ->
        let brush = SolidColorBrush(Colors.Black)
        let size = Cell 16.0
        let icon = IconSet.image Glyph.Copy brush size :?> Image
        let win = Window(Content = icon)
        win.Show()
        Assert.Equal(16.0, icon.Width)
        size.Set 20.0
        Assert.Equal(20.0, icon.Width)
        Assert.Equal(20.0, icon.Height)
        // 同一画笔同一字形复用同一图像；官方实现保留画笔实例，改色即跟随
        Assert.Same(icon.Source, IconSet.drawing Glyph.Copy brush)
        let pens =
            match icon.Source with
            | :? DrawingImage as d ->
                let rec walk (x: Drawing) =
                    match x with
                    | :? DrawingGroup as g -> g.Children |> Seq.collect walk |> List.ofSeq
                    | :? GeometryDrawing as gd -> [ gd ]
                    | _ -> []
                walk d.Drawing
            | _ -> []
        Assert.NotEmpty pens
        // 这一版 Lucide 以填充画笔出墨：断言用的就是我们传入的同一实例
        Assert.True(pens |> List.exists (fun gd -> obj.ReferenceEquals(gd.Brush, brush) || (not (isNull gd.Pen) && obj.ReferenceEquals(gd.Pen.Brush, brush))))
        win.Close())
