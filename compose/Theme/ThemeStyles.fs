namespace Wanxiang2.Theme

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Styling
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics

/// 主题安装的样式类名。控件库只挂类名，外观由这里查表（旧 Controls/Primitives.fs 与 Interaction.fs 的外观部分）。
module StyleClass =
    /// 参与统一状态反馈的表面（按压、禁用、焦点环、悬停变体都要求它）
    let surface = "surface"
    let hoverPlain = "hover-plain"
    let hoverAccent = "hover-accent"
    let hoverRaised = "hover-raised"
    let hoverDanger = "hover-danger"
    let hoverBordered = "hover-bordered"
    /// 输入壳：焦点环用浅一档的 accentSoft
    let focusSoft = "focus-soft"
    /// 程序化 / 初始聚焦来源（:focus-visible 只覆盖键盘）
    let focusByProgram = "focus-by-program"
    /// 文字按钮四档（与 surface、对应 hover 变体一起挂）
    let buttonPrimary = "button-primary"
    let buttonSecondary = "button-secondary"
    let buttonGhost = "button-ghost"
    let buttonDanger = "button-danger"
    /// 图标按钮与强调色实心图标按钮
    let iconButton = "icon-button"
    let iconButtonAccent = "icon-button-accent"
    /// 开关：轨道、拨钮、开启态
    let toggleTrack = "toggle-track"
    let toggleKnob = "toggle-knob"
    let toggleOn = "on"
    /// 分组卡、小标签三档、分隔线
    let groupingCard = "grouping-card"
    let tag = "tag"
    let tagWarning = "tag-warning"
    let tagDanger = "tag-danger"
    let hairline = "hairline"
    /// 分节标签（旧 Ui.sectionLabel；浮层菜单的分组标题）
    let sectionLabel = "section-label"
    /// 浮层菜单项底（透明，悬停由 hover-plain / hover-danger 给）
    let menuItem = "menu-item"
    /// 下拉选择器外壳（底与描边；悬停换描边由 hover-bordered 给）
    let selectButton = "select-button"

/// 把主题样式装进一个 Styles。样式只建一次：画笔是管理者的活实例（原地改色），
/// 尺寸、间距经 ToBinding 绑到管理者的活值，焦点环颜色是本对象自己的活值、随 Changed 更新。
/// （实测：清空再重填 Styles 不会让已挂的控件重新取值，所以不走重建。）
type ThemeStyles(manager: ThemeManager) =
    let group = Styles()
    let brush k = manager.Brush k :> IBrush
    let bind (o: IObservable<'T>) : obj = box (AvaloniaObjectExtensions.ToBinding o)
    let metric m = bind (manager.Metric m)
    let thick (f: float -> Thickness) (o: IObservable<float>) = bind (Live.map f o)
    let corner (s: Space) = bind (Live.map CornerRadius (manager.Space s :> IObservable<float>))
    let shadowOf (color: PaletteKey) =
        let v = LiveValue(BoxShadows(BoxShadow()))
        let refresh () =
            v.Set(BoxShadows(BoxShadow(Spread = (manager.Space Space.StrokeThick).Value, Color = manager.Color color)))
        refresh ()
        manager.Changed.Add(fun _ -> refresh ())
        (manager.Space Space.StrokeThick :> IObservable<float>).Subscribe(fun _ -> refresh ()) |> ignore
        v
    let ringAccent = shadowOf PaletteKey.Accent
    let ringSoft = shadowOf PaletteKey.AccentSoft
    let style (select: Selector -> Selector) (setters: (AvaloniaProperty * obj) list) =
        let st = Style(fun s -> select s)
        for p, v in setters do st.Setters.Add(Setter(p, v))
        st :> IStyle
    let border (cls: string) (s: Selector) = s.OfType<Border>().Class(cls)
    let surface (s: Selector) = s.OfType<Border>().Class(StyleClass.surface)
    let childText (cls: string) (s: Selector) = s.OfType<Border>().Class(cls).Child().OfType<TextBlock>()
    let thin () = thick Thickness (manager.Metric Metric.BorderWidth)

    let build () : IStyle list =
        let buttonBase cls (bg: IBrush) (fg: IBrush) (stroke: IBrush option) =
            [ style (border cls)
                  [ Border.BackgroundProperty, box bg
                    Border.BorderBrushProperty, (match stroke with Some b -> box b | None -> null)
                    Border.BorderThicknessProperty, (match stroke with Some _ -> thin () | None -> box (Thickness 0.0))
                    Border.CornerRadiusProperty, corner Space.RadiusMd
                    Border.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Space Space.X2l) (manager.Metric Metric.TextButtonPaddingY))
                    Layoutable.MinHeightProperty, (metric Metric.TextButtonMinHeight) ]
              style (childText cls)
                  [ TextBlock.FontSizeProperty, (metric Metric.FontSmall)
                    TextBlock.FontWeightProperty, box FontWeight.Medium
                    TextBlock.ForegroundProperty, box fg ] ]
        let iconSize () = metric Metric.IconButton
        let tagTone cls (stroke: PaletteKey) (ink: PaletteKey) (bg: PaletteKey) =
            [ style (border cls) [ Border.BorderBrushProperty, box (brush stroke); Border.BackgroundProperty, box (brush bg) ]
              style (childText cls) [ TextBlock.ForegroundProperty, box (brush ink) ] ]
        [ // ---- 外观（旧 Primitives）----
          yield! buttonBase StyleClass.buttonPrimary (brush PaletteKey.Accent) (brush PaletteKey.TextOnAccent) None
          yield! buttonBase StyleClass.buttonSecondary (brush PaletteKey.Surface) (brush PaletteKey.Text) (Some(brush PaletteKey.Border))
          yield! buttonBase StyleClass.buttonGhost Brushes.Transparent (brush PaletteKey.TextMuted) None
          yield! buttonBase StyleClass.buttonDanger (brush PaletteKey.DangerSoft) (brush PaletteKey.Danger) None
          for cls, radius, bg in [ StyleClass.iconButton, Space.RadiusMd, Brushes.Transparent :> IBrush
                                   StyleClass.iconButtonAccent, Space.RadiusPill, brush PaletteKey.Accent ] do
              yield style (border cls)
                  [ Layoutable.WidthProperty, iconSize (); Layoutable.HeightProperty, iconSize ()
                    Layoutable.MinWidthProperty, iconSize (); Layoutable.MinHeightProperty, iconSize ()
                    Border.CornerRadiusProperty, corner radius
                    Border.BackgroundProperty, box bg ]
          yield style (border StyleClass.toggleTrack)
              [ Layoutable.WidthProperty, (metric Metric.ToggleTrackWidth)
                Layoutable.HeightProperty, (metric Metric.ToggleTrackHeight)
                Border.CornerRadiusProperty, corner Space.RadiusPill
                Border.BackgroundProperty, box (brush PaletteKey.Line)
                Border.PaddingProperty, thick (fun x -> Thickness(x, 0.0)) (manager.Metric Metric.ToggleKnobInsetX) ]
          yield style (fun s -> s.OfType<Border>().Class(StyleClass.toggleTrack).Class(StyleClass.toggleOn))
              [ Border.BackgroundProperty, box (brush PaletteKey.Accent) ]
          yield style (border StyleClass.toggleKnob)
              [ Layoutable.WidthProperty, (metric Metric.ToggleKnobSize)
                Layoutable.HeightProperty, (metric Metric.ToggleKnobSize)
                Border.CornerRadiusProperty, corner Space.RadiusPill
                Border.BackgroundProperty, box (brush PaletteKey.SurfaceRaised) ]
          yield style (border StyleClass.groupingCard)
              [ Border.BackgroundProperty, box (brush PaletteKey.SurfaceContainer)
                Border.BorderBrushProperty, box (brush PaletteKey.Hairline)
                Border.BorderThicknessProperty, thin ()
                Border.CornerRadiusProperty, corner Space.RadiusLg ]
          yield style (border StyleClass.tag)
              [ Border.BorderThicknessProperty, thin ()
                Border.CornerRadiusProperty, corner Space.RadiusSm
                Border.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Metric Metric.TagPaddingX) (manager.Space Space.XXs)) ]
          yield style (childText StyleClass.tag) [ TextBlock.FontSizeProperty, (metric Metric.FontMicro) ]
          yield! tagTone StyleClass.tag PaletteKey.Border PaletteKey.TextMuted PaletteKey.SurfaceRaised
          yield! tagTone StyleClass.tagWarning PaletteKey.Warning PaletteKey.Warning PaletteKey.SurfaceRaised
          yield! tagTone StyleClass.tagDanger PaletteKey.Danger PaletteKey.Danger PaletteKey.DangerSoft
          yield style (border StyleClass.hairline)
              [ Layoutable.HeightProperty, (metric Metric.BorderWidth); Border.BackgroundProperty, box (brush PaletteKey.BorderSoft) ]
          // 静止态的底必须由样式给：本地值会压住后面的悬停样式。
          yield style (border StyleClass.menuItem) [ Border.BackgroundProperty, box Brushes.Transparent ]
          yield style (border StyleClass.selectButton)
              [ Border.BackgroundProperty, box (brush PaletteKey.Surface)
                Border.BorderBrushProperty, box (brush PaletteKey.Border) ]
          yield style (fun s -> s.OfType<TextBlock>().Class(StyleClass.sectionLabel))
              [ TextBlock.FontSizeProperty, (metric Metric.FontMicro)
                TextBlock.FontWeightProperty, box FontWeight.Medium
                TextBlock.ForegroundProperty, box (brush PaletteKey.TextFaint)
                TextBlock.LetterSpacingProperty, bind (manager.Space Space.TrackingSection) ]
          // ---- 状态（旧 Interaction）：后注册者优先，焦点 > 悬停 > 按压/禁用 ----
          yield style (fun s -> (surface s).Class(":pressed")) [ Visual.OpacityProperty, (metric Metric.OpacityPressed) ]
          yield style (fun s -> (surface s).Class(":disabled"))
              [ Visual.OpacityProperty, (metric Metric.OpacityDisabled); Avalonia.Input.InputElement.IsHitTestVisibleProperty, box false ]
          for variant, over in [ StyleClass.hoverPlain, brush PaletteKey.Hover
                                 StyleClass.hoverAccent, brush PaletteKey.AccentHover
                                 StyleClass.hoverRaised, manager.Derived DerivedBrush.HoverRaised :> IBrush
                                 StyleClass.hoverDanger, manager.Derived DerivedBrush.HoverDanger :> IBrush ] do
              yield style (fun s -> (surface s).Class(variant).Class(":pointerover")) [ Border.BackgroundProperty, box over ]
          yield style (fun s -> (surface s).Class(StyleClass.hoverBordered).Class(":pointerover"))
              [ Border.BorderBrushProperty, box (brush PaletteKey.Line) ]
          // 焦点环颜色写进 BoxShadow 是当时的值：切主题时 Restyle 重建
          yield style (fun s -> (surface s).Class(StyleClass.focusSoft).Class(":focus-within"))
              [ Border.BoxShadowProperty, bind ringSoft ]
          yield style (fun s -> (surface s).Class(":focus-visible")) [ Border.BoxShadowProperty, bind ringAccent ]
          yield style (fun s -> (surface s).Class(StyleClass.focusByProgram).Class(":focus"))
              [ Border.BoxShadowProperty, bind ringAccent ] ]

    do for s in build () do group.Add s

    /// 本对象拥有的样式组
    member _.Styles = group

    /// 装进应用或窗口的 Styles；同一目标装多次无害
    member _.Install(target: Styles) = if not (target.Contains group) then target.Add group

    /// 让一个表面参与统一状态反馈：挂 surface 与悬停变体，并挂状态过渡（只补间颜色）
    member _.Surface(control: Border, variant: string) =
        control.Classes.Add StyleClass.surface
        if not (System.String.IsNullOrEmpty variant) then control.Classes.Add variant
        control.Transitions <- manager.StateTransitions((variant = StyleClass.hoverBordered))
