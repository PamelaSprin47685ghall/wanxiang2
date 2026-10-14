module Wanxiang2.ThemeFonts.Tests.Tests

open System
open System.IO
open System.Security.Cryptography
open Avalonia.Media
open Avalonia.Platform
open Xunit
open Wanxiang2.ThemeFonts

let private golden =
    let dir = Path.Combine(__SOURCE_DIRECTORY__, "golden")
    File.ReadAllLines(Path.Combine(dir, "fonts.sha256"))
    |> Array.filter (fun l -> l.Trim() <> "")
    |> Array.map (fun l -> let p = l.Split("  ") in p.[1], p.[0])

[<Fact>]
let ``every old font file ships byte for byte`` () =
    Headless.run (fun () ->
        for name, sha in golden do
            use s = AssetLoader.Open(Uri(ThemeFonts.directory + name))
            let hash = Convert.ToHexString(SHA256.HashData s).ToLowerInvariant()
            Assert.True((hash = sha), name))

let private resolves (family: FontFamily) (expected: string) =
    let tf = Typeface(family)
    let ok, glyph = FontManager.Current.TryGetGlyphTypeface(tf)
    Assert.True(ok, expected)
    Assert.Equal(expected, glyph.FamilyName)

[<Fact>]
let ``body mono and katex families resolve to the embedded faces`` () =
    Headless.run (fun () ->
        resolves (FontFamily ThemeFonts.body) "Sarasa Gothic SC"
        resolves (FontFamily ThemeFonts.mono) "Sarasa Term SC"
        resolves (ThemeFonts.family "KaTeX_Main") "KaTeX_Main"
        resolves (ThemeFonts.family "KaTeX_Size4") "KaTeX_Size4")

[<Fact>]
let ``katex main carries the minus and plus minus glyphs`` () =
    Headless.run (fun () ->
        let _, glyph = FontManager.Current.TryGetGlyphTypeface(Typeface(ThemeFonts.family "KaTeX_Main"))
        for c in [ '\u2212'; '\u00B1' ] do
            Assert.NotEqual(0us, glyph.CharacterToGlyphMap.[int c]))
