module Wanxiang2.HostSwitcher.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.HostSwitcher
open Wanxiang2.HostSwitcher.Tests.Probe

let private look () =
    let bar = Cell 52.0
    bar,
    { Text = Looks.brush Colors.Black; Muted = Looks.brush Colors.Gray; Faint = Looks.brush Colors.LightGray
      Accent = Looks.brush Colors.Blue; Danger = Looks.brush Colors.Red; Success = Looks.brush Colors.Green
      Warning = Looks.brush Colors.Orange; FieldSurface = Looks.brush Colors.White; FieldEdge = Looks.brush Colors.Silver
      BodyFontSize = Cell 14.0; SmallFontSize = Cell 13.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 11.0
      HeadingFontSize = Cell 17.0; TitleFontSize = Cell 19.0; DisplayFontSize = Cell 28.0; CaptionLineHeight = Cell 17.0
      LabelTracking = Cell 0.2; SpaceXs = Cell 4.0; SpaceSm = Cell 6.0; SpaceMd = Cell 8.0; SpaceXl = Cell 16.0
      Space2xl = Cell 20.0; Space4xl = Cell 32.0; RadiusMd = Cell 8.0; RadiusLg = Cell 12.0; BorderWidth = Cell 1.0
      FieldInsetX = Cell 4.0; TextFieldPaddingY = Cell 6.0; TextFieldTextMinHeight = Cell 22.0; SettingsRowMinHeight = Cell 52.0
      MonoFont = Cell(FontFamily "monospace"); Glyph = Looks.glyph
      DotSize = Cell 8.0; BarHeight = bar; DividerWidth = Cell 1.0; IconButtonSize = Cell 30.0; CompactTarget = Cell 44.0
      TextButtonMinHeight = Cell 36.0; Hairline = Looks.brush Colors.Gainsboro; RadiusSm = Cell 6.0 }

let private make () =
    let bar, l = look ()
    let h = HostSwitcher l
    let w = host h.View
    h, w, bar

[<Fact>]
let ``host switcher shows authority, connection text and exposes reconnect invoke`` () =
    Headless.run (fun () ->
        let h, _, _ = make ()
        h.SetServerUrl "https://example.com:8443/path"
        h.SetConnection(true, "已连接")
        Assert.Contains("example.com:8443", textOf h.View)
        Assert.Equal("已连接", h.ConnectionText)
        Assert.True h.IsConnected
        let n = ref 0
        h.ReconnectClicked.Add(fun () -> n.Value <- n.Value + 1)
        click h.StatusRow
        Assert.Equal(1, n.Value)
        h.SetServerUrl "not-a-url"
        Assert.Contains("选择主机", textOf h.View))

[<Fact>]
let ``settings and palette buttons fire and compact raises their targets`` () =
    Headless.run (fun () ->
        let h, _, bar = make ()
        h.ConfigurePalette true
        Assert.True h.PaletteButton.IsVisible
        let log = ResizeArray<string>()
        h.SettingsClicked.Add(fun () -> log.Add "settings")
        h.PaletteClicked.Add(fun () -> log.Add "palette")
        click h.SettingsButton
        click h.PaletteButton
        Assert.Equal<string list>([ "settings"; "palette" ], List.ofSeq log)
        Assert.Equal(30.0, h.SettingsButton.Width)
        h.SetCompact true
        Assert.Equal(44.0, h.SettingsButton.Width)
        bar.Set 60.0
        Assert.Equal(60.0, (h.View :?> Border).Height))
