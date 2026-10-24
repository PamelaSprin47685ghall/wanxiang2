module Wanxiang2.IconSet.Tests.Tests

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.VisualTree
open Xunit
open IconPacks.Avalonia.Lucide
open Wanxiang2.IconSet

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
        Assert.True(
            pens
            |> List.exists (fun gd ->
                obj.ReferenceEquals(gd.Brush, brush)
                || (not (isNull gd.Pen) && obj.ReferenceEquals(gd.Pen.Brush, brush)))
        )

        win.Close())

[<Fact>]
let ``the lucide control is the official glyph, not a drawn path`` () =
    Headless.run (fun () ->
        Assert.Equal(PackIconLucideKind.LoaderCircle, IconSet.kind Glyph.Loader)
        Assert.Contains(Glyph.Loader, IconSet.all)
        let brush = SolidColorBrush(Colors.Black)
        let icon = IconSet.control Glyph.PanelLeft 14.0
        icon.Tag <- "panel"
        icon.Foreground <- brush
        let win = Window(Content = icon, Width = 32.0, Height = 32.0)
        win.Show()
        Assert.Equal(14.0, icon.Width)
        Assert.Equal(14.0, icon.Height)
        Assert.Equal(box "panel", icon.Tag)

        let image =
            icon.GetVisualDescendants()
            |> Seq.tryPick (function
                | :? Image as i -> Some i
                | _ -> None)

        match image with
        | None -> failwith "lucide glyph did not produce an image"
        | Some i -> Assert.Same(i.Source, IconSet.drawing Glyph.PanelLeft brush)

        win.Close())
