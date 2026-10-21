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
    /// 画笔由主题管理者给（同一实例，切主题原地改色）；字号、行高、间距由对话框控件侧走动态资源键。
    let importOf (t: ThemeManager) : ImportSheetAppearance =
        let brush k = t.Brush k :> IBrush

        {
            Text = brush PaletteKey.Text
            Muted = brush PaletteKey.TextMuted
            Danger = brush PaletteKey.Danger
        }

    let exportOf (t: ThemeManager) : ExportSheetAppearance =
        let brush k = t.Brush k :> IBrush

        {
            Text = brush PaletteKey.Text
            Muted = brush PaletteKey.TextMuted
            Success = brush PaletteKey.Success
            Danger = brush PaletteKey.Danger
        }

/// 导入导出用到的时长与减弱动效（主题管理者拥有）
type TransferTiming =
    {
        ImportResponseTimeout: unit -> TimeSpan
        ExportExpiryCheckTick: unit -> TimeSpan
        ProgressTextThrottle: unit -> TimeSpan
        After: TimeSpan -> (unit -> unit) -> IDisposable
        ReducedMotion: unit -> bool
        /// 对话层宽（主题度量 TransferDialogWidth）
        DialogWidth: unit -> float
    }

module TransferTiming =
    let ofThemeWithAfter (t: ThemeManager) (after: TimeSpan -> (unit -> unit) -> IDisposable) =
        let ms timing () =
            TimeSpan.FromMilliseconds (t.Timing timing).Value

        {
            ImportResponseTimeout = ms Timing.ImportResponseTimeout
            ExportExpiryCheckTick = ms Timing.ExportExpiryCheckTick
            ProgressTextThrottle = ms Timing.ProgressTextThrottle
            After = after
            ReducedMotion = fun () -> t.Motion.IsReduced
            DialogWidth = fun () -> (t.Metric Metric.TransferDialogWidth).Value
        }

    let ofTheme (t: ThemeManager) =
        let defaultAfter span (action: unit -> unit) =
            let timer =
                new System.Threading.Timer(
                    (fun _ ->
                        Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(fun () -> action ())
                        |> ignore),
                    null,
                    span,
                    System.Threading.Timeout.InfiniteTimeSpan
                )

            { new IDisposable with
                member _.Dispose() = timer.Dispose()
            }

        ofThemeWithAfter t defaultAfter
