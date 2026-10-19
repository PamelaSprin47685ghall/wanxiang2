module Wanxiang2.BrandMark.Tests.Tests

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Platform
open Xunit
open Wanxiang2.BrandMark

// 迁自旧 VisualPassR3Tests `品牌标有图时不垫白底`。
[<Fact>]
let ``品牌标有图时不垫白底`` () =
    Headless.run (fun () ->
        let bitmap = new WriteableBitmap(PixelSize(4, 4), Vector(96.0, 96.0), Avalonia.Platform.PixelFormat.Bgra8888, AlphaFormat.Premul)
        match BrandMark.tile (Some(bitmap :> IImage)) 48.0 with
        | :? Border as tile ->
            Assert.IsType<Image>(tile.Child) |> ignore
            Assert.Equal(Stretch.Uniform, (tile.Child :?> Image).Stretch)
            Assert.Equal(Brushes.Transparent :> IBrush, tile.Background)
        | other -> failwithf "unexpected %A" other)

[<Fact>]
let ``no image falls back to the glyph on the live surface`` () =
    Headless.run (fun () ->
        let tile = BrandMark.tile None 48.0 :?> Border
        let glyph = tile.Child :?> TextBlock
        Assert.Equal("万", glyph.Text)
        Assert.Equal(48.0 * 0.42, glyph.FontSize, 3)
        Assert.True(tile.Classes.Contains "surface")
        Assert.True(glyph.Classes.Contains "brand-glyph")
        Assert.Equal(48.0, tile.Width)
        Assert.Equal(CornerRadius(48.0 * 0.22), tile.CornerRadius))

[<Fact>]
let ``loading tries the asset then local files in order`` () =
    Headless.run (fun () ->
        Assert.True((BrandMark.tryLoad (Some(Uri "avares://Nope/missing.png")) [ "/no/such/logo.png" ]).IsNone)
        let path = IO.Path.Combine(IO.Path.GetTempPath(), sprintf "brand-%O.png" (Guid.NewGuid()))
        // 1×1 透明 PNG
        IO.File.WriteAllBytes(path, Convert.FromBase64String "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==")
        try
            let loaded = BrandMark.tryLoad None [ "/no/such/logo.png"; path ]
            Assert.Equal(Some(PixelSize(1, 1)), loaded |> Option.map _.PixelSize)
        finally IO.File.Delete path)
