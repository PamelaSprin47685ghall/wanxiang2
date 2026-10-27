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
        Assert.Contains("Noto Sans", ThemeFonts.body)
        Assert.Contains("Sarasa Gothic SC", ThemeFonts.body)
        Assert.Contains("Noto Serif", ThemeFonts.display)

        let ok, bodyGlyph =
            FontManager.Current.TryGetGlyphTypeface(Typeface(FontFamily ThemeFonts.body))

        Assert.True(ok, "body")

        Assert.True(
            [ "Noto"; "PingFang SC"; "Hiragino Sans GB"; "Microsoft YaHei"; "Sarasa" ]
            |> List.exists bodyGlyph.FamilyName.Contains,
            bodyGlyph.FamilyName
        )

        resolves (FontFamily(ThemeFonts.directory + "#Sarasa Gothic SC")) "Sarasa Gothic SC"
        resolves (FontFamily ThemeFonts.mono) "Sarasa Term SC"
        resolves (ThemeFonts.family "KaTeX_Main") "KaTeX_Main"
        resolves (ThemeFonts.family "KaTeX_Size4") "KaTeX_Size4")

[<Fact>]
let ``the symbol family resolves to the embedded face`` () =
    Headless.run (fun () -> resolves (FontFamily ThemeFonts.symbols) "Wanxiang Symbols")

/// 复合字族的逐字回落：字体管理器按**字符**在复合字族的各段里找能覆盖该码位的字族。
/// 这里查的是「匹配到的字形不是 .notdef」，而不是「字族名」。
let private covers (family: string) (codepoints: int list) =
    let f = FontFamily family

    for cp in codepoints do
        let matched, typeface =
            FontManager.Current.TryMatchCharacter(cp, FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, f, null)

        Assert.True(matched, sprintf "U+%04X 匹配不到字族" cp)
        let ok, glyph = FontManager.Current.TryGetGlyphTypeface(typeface)
        Assert.True(ok, sprintf "U+%04X 匹配到的字族没有字形" cp)
        Assert.NotEqual(0us, glyph.CharacterToGlyphMap.[cp])

[<Fact>]
let ``body and mono fall back to real glyphs for the symbol codepoints`` () =
    Headless.run (fun () ->
        let symbols =
            [ 0x2315; 0x2318; 0x2699; 0x270E; 0x2713; 0x2715; 0x1F4E6; 0x1F50D; 0x1F5D1 ]

        covers ThemeFonts.body symbols
        covers ThemeFonts.mono symbols)

[<Fact>]
let ``katex main carries the minus and plus minus glyphs`` () =
    Headless.run (fun () ->
        let _, glyph =
            FontManager.Current.TryGetGlyphTypeface(Typeface(ThemeFonts.family "KaTeX_Main"))

        for c in [ '\u2212'; '\u00B1' ] do
            Assert.NotEqual(0us, glyph.CharacterToGlyphMap.[int c]))
