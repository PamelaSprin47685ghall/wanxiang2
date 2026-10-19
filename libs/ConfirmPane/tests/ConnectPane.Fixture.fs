namespace Wanxiang2.ConnectPane.Tests

open System
open System.Collections.Generic
open Avalonia.Media
open Wanxiang2.ConnectPane
open Wanxiang2.ConnectPane.Tests.Probe

/// 由 Look.fs 生成的测试外观：活尺寸都在 Cells 里，测试可改值
module Fixture =
    let make () =
        let cells = Dictionary<string, Cell<float>>()
        let cell name (v: float) =
            let c = Cell v
            cells[name] <- c
            c :> IObservable<float>
        let look =
            { Text = Looks.brush Colors.Gray
              Muted = Looks.brush Colors.Gray
              Faint = Looks.brush Colors.Gray
              Accent = Looks.brush Colors.Gray
              Danger = Looks.brush Colors.Gray
              Success = Looks.brush Colors.Gray
              Warning = Looks.brush Colors.Gray
              FieldSurface = Looks.brush Colors.Gray
              FieldEdge = Looks.brush Colors.Gray
              BodyFontSize = cell "BodyFontSize" 14.0
              SmallFontSize = cell "SmallFontSize" 13.0
              CaptionFontSize = cell "CaptionFontSize" 12.0
              MicroFontSize = cell "MicroFontSize" 11.0
              HeadingFontSize = cell "HeadingFontSize" 17.0
              TitleFontSize = cell "TitleFontSize" 19.0
              DisplayFontSize = cell "DisplayFontSize" 28.0
              CaptionLineHeight = cell "CaptionLineHeight" 10.0
              LabelTracking = cell "LabelTracking" 10.0
              SpaceXs = cell "SpaceXs" 10.0
              SpaceSm = cell "SpaceSm" 10.0
              SpaceMd = cell "SpaceMd" 10.0
              SpaceXl = cell "SpaceXl" 10.0
              Space2xl = cell "Space2xl" 10.0
              Space4xl = cell "Space4xl" 10.0
              RadiusMd = cell "RadiusMd" 10.0
              RadiusLg = cell "RadiusLg" 10.0
              BorderWidth = cell "BorderWidth" 10.0
              FieldInsetX = cell "FieldInsetX" 10.0
              TextFieldPaddingY = cell "TextFieldPaddingY" 10.0
              TextFieldTextMinHeight = cell "TextFieldTextMinHeight" 10.0
              SettingsRowMinHeight = cell "SettingsRowMinHeight" 10.0
              MonoFont = Cell(FontFamily "monospace")
              Glyph = Looks.glyph
              SpaceXXs = cell "SpaceXXs" 10.0
              Hairline = Looks.brush Colors.Gray }
        look, cells
