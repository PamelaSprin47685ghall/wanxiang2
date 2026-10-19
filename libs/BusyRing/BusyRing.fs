namespace Wanxiang2.BusyRing

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Shapes
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media

/// 动态资源绑定辅助
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 忙碌转圈（原生 Viewbox + Canvas + Path）：16×16 画布上的弧经 Viewbox 缩到目标尺寸；
/// 转动走声明式 anim-spin 样式类，无 DispatcherTimer，无 Look，支持单入口 Render(busy: bool)。
type BusyRing(?size: float) =
    let targetSize = defaultArg size 16.0
    let track =
        Path(
            Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 1 1 7.99 1.6",
            Fill = Brushes.Transparent,
            Stretch = Stretch.None,
            StrokeThickness = 1.8,
            IsVisible = false)
    let arc =
        Path(
            Data = Geometry.Parse "M 8 1.6 A 6.4 6.4 0 0 1 14.4 8",
            StrokeLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.None,
            StrokeThickness = 1.8)
    let canvas = Canvas(Width = 16.0, Height = 16.0)
    let view =
        Viewbox(
            Width = targetSize,
            Height = targetSize,
            Stretch = Stretch.Uniform,
            Child = canvas,
            RenderTransformOrigin = RelativePoint.Center)
    let mutable isBusy = true

    do
        track.Bind(Path.StrokeProperty, Dyn.res "Palette.Border") |> ignore
        arc.Bind(Path.StrokeProperty, Dyn.res "Palette.Accent") |> ignore
        canvas.Children.Add track
        canvas.Children.Add arc
        view.Classes.Add "anim-spin"

    /// 放进布局的原生控件宿主
    member _.Host: Control = view :> Control
    /// 是否正处于忙碌态
    member _.IsBusy = isBusy
    /// 旋转样式类是否已挂载
    member _.IsSpinning = view.Classes.Contains "anim-spin" && view.IsVisible

    /// 单入口幂等渲染：控制忙碌旋转态与可见性
    member _.Render(busy: bool) =
        isBusy <- busy
        view.IsVisible <- busy
        if busy then
            if not (view.Classes.Contains "anim-spin") then view.Classes.Add "anim-spin"
        else
            view.Classes.Remove "anim-spin" |> ignore
