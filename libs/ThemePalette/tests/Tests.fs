module Wanxiang2.ThemePalette.Tests.Tests

open System
open System.IO
open Xunit
open Wanxiang2.ThemePalette

let private name (k: PaletteKey) = sprintf "%A" k
let private color mode key =
    let k = ThemePalette.keys |> List.find (fun k -> name k = key)
    ThemePalette.color (if mode = "Light" then PaletteMode.Light else PaletteMode.Dark) k

/// 金样由旧 Design/Palette.fs（照抄）逐项生成
[<Fact>]
let ``golden: every color matches the old palette`` () =
    let expected = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "themepalette.txt"))
    Assert.Equal(expected, Render.render (ThemePalette.keys |> List.map name) color)

[<Fact>]
let ``both modes cover every key and differ on the canvas`` () =
    Assert.Equal(ThemePalette.keys.Length, ThemePalette.keys |> List.distinct |> List.length)
    Assert.NotEqual(ThemePalette.color PaletteMode.Light PaletteKey.Canvas, ThemePalette.color PaletteMode.Dark PaletteKey.Canvas)

[<Fact>]
let ``surface soft follows the surface of the same mode`` () =
    for mode in [ PaletteMode.Light; PaletteMode.Dark ] do
        let s = ThemePalette.color mode PaletteKey.Surface
        let soft = ThemePalette.color mode PaletteKey.SurfaceSoft
        Assert.Equal((0x55uy, s.R, s.G, s.B), (soft.A, soft.R, soft.G, soft.B))
