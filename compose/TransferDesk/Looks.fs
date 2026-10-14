namespace Wanxiang2.TransferDesk

open System
open Avalonia.Media
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.Theme
open Wanxiang2.ImportSheet
open Wanxiang2.ExportSheet

/// 从主题管理者取两张对话框的外观与时长。
module TransferLook =
    let importOf (t: ThemeManager) : ImportSheetLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Danger = brush PaletteKey.Danger
          TitleFontSize = metric Metric.FontTitle
          BodyFontSize = metric Metric.FontBody
          SmallFontSize = metric Metric.FontSmall
          CaptionFontSize = metric Metric.FontCaption
          CaptionLineHeight = LiveValue ReadingRhythm.captionLineHeight
          StackSpacing = t.Space Space.Xl
          ButtonSpacing = t.Space Space.Md }

    let exportOf (t: ThemeManager) : ExportSheetLook =
        let brush k = t.Brush k :> IBrush
        let metric m = t.Metric m :> IObservable<float>
        { Text = brush PaletteKey.Text
          Muted = brush PaletteKey.TextMuted
          Success = brush PaletteKey.Success
          Danger = brush PaletteKey.Danger
          TitleFontSize = metric Metric.FontTitle
          BodyFontSize = metric Metric.FontBody
          CaptionFontSize = metric Metric.FontCaption
          CaptionLineHeight = LiveValue ReadingRhythm.captionLineHeight
          StackSpacing = t.Space Space.Xl
          ButtonSpacing = t.Space Space.Md }

/// 导入导出用到的时长与减弱动效（主题管理者拥有）
type TransferTiming =
    { ImportResponseTimeout: unit -> TimeSpan
      ExportExpiryCheckTick: unit -> TimeSpan
      ProgressTextThrottle: unit -> TimeSpan
      ReducedMotion: unit -> bool }

module TransferTiming =
    let ofTheme (t: ThemeManager) =
        let ms timing () = TimeSpan.FromMilliseconds (t.Timing timing).Value
        { ImportResponseTimeout = ms Timing.ImportResponseTimeout
          ExportExpiryCheckTick = ms Timing.ExportExpiryCheckTick
          ProgressTextThrottle = ms Timing.ProgressTextThrottle
          ReducedMotion = fun () -> t.Motion.IsReduced }
