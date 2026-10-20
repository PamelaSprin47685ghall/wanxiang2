namespace Wanxiang2.Theme

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Presenters
open Avalonia.Controls.Primitives
open Avalonia.Controls.Templates
open Avalonia.Layout
open Avalonia.Media
open Avalonia.Styling
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch

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
    /// 输入框边界描边：WCAG 1.4.11 要求 ≥ 3:1
    let controlStroke = "control-stroke"
    /// 程序化 / 初始聚焦来源（:focus-visible 只覆盖键盘）
    let focusByProgram = "focus-by-program"
    /// 原生控件主次档样式类（供 Button / ToggleButton / SplitButton 等）
    let primary = "primary"
    let secondary = "secondary"
    let ghost = "ghost"
    let danger = "danger"
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
    let textArea = "text-area"
    let codeBox = "code-box"
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
    /// 声明式动效样式类（挂在 Visual / Control 上）
    let animBreathe = "anim-breathe"
    let animSpin = "anim-spin"
    let animFadeIn = "anim-fade-in"

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
    let buttonCls (cls: string) (s: Selector) = s.Is<Button>().Class(cls)
    let buttonSurface (s: Selector) = s.Is<Button>().Class(StyleClass.surface)
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
          // ---- 外观（Button 平行路径）----
          yield style (buttonCls StyleClass.buttonGhost)
              [ Button.BackgroundProperty, box Brushes.Transparent
                Button.ForegroundProperty, box (brush PaletteKey.TextMuted)
                Button.BorderBrushProperty, box Brushes.Transparent
                Button.BorderThicknessProperty, box (Thickness 0.0)
                Button.CornerRadiusProperty, corner Space.RadiusMd
                Button.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Space Space.X2l) (manager.Metric Metric.TextButtonPaddingY))
                Layoutable.MinHeightProperty, (metric Metric.TextButtonMinHeight) ]
          for cls, radius, bg in [ StyleClass.iconButton, Space.RadiusMd, Brushes.Transparent :> IBrush
                                   StyleClass.iconButtonAccent, Space.RadiusPill, brush PaletteKey.Accent ] do
              yield style (buttonCls cls)
                  [ Layoutable.WidthProperty, iconSize (); Layoutable.HeightProperty, iconSize ()
                    Layoutable.MinWidthProperty, iconSize (); Layoutable.MinHeightProperty, iconSize ()
                    Button.CornerRadiusProperty, corner radius
                    Button.BackgroundProperty, box bg ]
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
              [ Border.BoxShadowProperty, bind ringAccent ]
          // ---- Button 状态样式（平行路径：按压/禁用/悬停/焦点环）----
          yield style (fun s -> (buttonSurface s).Class(":pressed")) [ Visual.OpacityProperty, (metric Metric.OpacityPressed) ]
          yield style (fun s -> (buttonSurface s).Class(":disabled"))
              [ Visual.OpacityProperty, (metric Metric.OpacityDisabled); Avalonia.Input.InputElement.IsHitTestVisibleProperty, box false ]
          for variant, over in [ StyleClass.hoverPlain, brush PaletteKey.Hover
                                 StyleClass.hoverAccent, brush PaletteKey.AccentHover
                                 StyleClass.hoverRaised, manager.Derived DerivedBrush.HoverRaised :> IBrush
                                 StyleClass.hoverDanger, manager.Derived DerivedBrush.HoverDanger :> IBrush ] do
              yield style (fun s -> (buttonSurface s).Class(variant).Class(":pointerover")) [ Button.BackgroundProperty, box over ]
          yield style (fun s -> (buttonSurface s).Class(StyleClass.hoverBordered).Class(":pointerover"))
              [ Button.BorderBrushProperty, box (brush PaletteKey.Line) ]
          yield style (fun s -> (buttonSurface s).Class(StyleClass.focusSoft).Class(":focus-within"))
              [ Button.BorderBrushProperty, box (brush PaletteKey.AccentSoft) ]
          yield style (fun s -> (buttonSurface s).Class(":focus-visible"))
              [ Button.BorderBrushProperty, box (brush PaletteKey.Accent)
                Button.BorderThicknessProperty, thick Thickness (manager.Metric Metric.BorderWidth) ]
          yield style (fun s -> (buttonSurface s).Class(StyleClass.focusByProgram).Class(":focus"))
              [ Button.BorderBrushProperty, box (brush PaletteKey.Accent)
                Button.BorderThicknessProperty, thick Thickness (manager.Metric Metric.BorderWidth) ]

          // ========================================================
          // ---- 原生控件覆盖与样式（AGENTS.md 规则 1 & 2）----
          // ========================================================

          // 1. 去除原生 Button / ToggleButton 的默认焦点环（FocusAdorner），由自定义样式提供聚焦反馈
          yield style (fun s -> s.OfType<Button>())
              [ Button.FocusAdornerProperty, null
                Layoutable.MinHeightProperty, (metric Metric.TextButtonMinHeight)
                Button.CornerRadiusProperty, corner Space.RadiusMd ]

          // 2. 原生 Button / ToggleButton 的四档样式（primary / secondary / ghost / danger）
          // 既支持新短类名（primary），也兼容旧长类名（button-primary）
          let buttonNative cls longCls (bg: IBrush) (fg: IBrush) (stroke: IBrush option) (hoverBg: IBrush) (hoverStroke: IBrush option) =
              let sel (s: Selector) = s.OfType<Button>().Class(cls)
              let selLong (s: Selector) = s.OfType<Button>().Class(longCls)
              let hoverSel (s: Selector) = s.OfType<Button>().Class(cls).Class(":pointerover")
              let hoverLongSel (s: Selector) = s.OfType<Button>().Class(longCls).Class(":pointerover")
              let pressedSel (s: Selector) = s.OfType<Button>().Class(cls).Class(":pressed")
              let pressedLongSel (s: Selector) = s.OfType<Button>().Class(longCls).Class(":pressed")
              let disabledSel (s: Selector) = s.OfType<Button>().Class(cls).Class(":disabled")
              let disabledLongSel (s: Selector) = s.OfType<Button>().Class(longCls).Class(":disabled")
              let borderBrushVal = match stroke with Some b -> box b | None -> box Brushes.Transparent
              let borderThickVal = match stroke with Some _ -> thin () | None -> box (Thickness 0.0)
              let hoverBorderVal = match hoverStroke with Some b -> box b | None -> borderBrushVal
              [ for s in [ sel; selLong ] do
                    yield style s
                        [ Button.BackgroundProperty, box bg
                          Button.ForegroundProperty, box fg
                          Button.BorderBrushProperty, borderBrushVal
                          Button.BorderThicknessProperty, borderThickVal
                          Button.CornerRadiusProperty, corner Space.RadiusMd
                          Button.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Space Space.X2l) (manager.Metric Metric.TextButtonPaddingY))
                          Layoutable.MinHeightProperty, (metric Metric.TextButtonMinHeight)
                          Button.FontSizeProperty, (metric Metric.FontSmall)
                          Button.FontWeightProperty, box FontWeight.Medium ]
                for s in [ hoverSel; hoverLongSel ] do
                    yield style s
                        [ Button.BackgroundProperty, box hoverBg
                          Button.BorderBrushProperty, hoverBorderVal ]
                for s in [ pressedSel; pressedLongSel ] do
                    yield style s [ Visual.OpacityProperty, (metric Metric.OpacityPressed) ]
                for s in [ disabledSel; disabledLongSel ] do
                    yield style s [ Visual.OpacityProperty, (metric Metric.OpacityDisabled) ] ]

          yield! buttonNative StyleClass.primary StyleClass.buttonPrimary (brush PaletteKey.Accent) (brush PaletteKey.TextOnAccent) None (brush PaletteKey.AccentHover) None
          yield! buttonNative StyleClass.secondary StyleClass.buttonSecondary (brush PaletteKey.Surface) (brush PaletteKey.Text) (Some(brush PaletteKey.Border)) (brush PaletteKey.Hover) (Some(brush PaletteKey.Line))
          yield! buttonNative StyleClass.ghost StyleClass.buttonGhost Brushes.Transparent (brush PaletteKey.TextMuted) None (brush PaletteKey.Hover) None
          yield! buttonNative StyleClass.danger StyleClass.buttonDanger (brush PaletteKey.DangerSoft) (brush PaletteKey.Danger) None (manager.Derived DerivedBrush.HoverDanger :> IBrush) None

          // 3. 原生 Button / ToggleButton 的键盘焦点环覆盖（:focus-visible 去默认蓝框，描边接 ControlStroke / Accent）
          yield style (fun s -> s.OfType<Button>().Class(":focus-visible"))
              [ Button.BorderBrushProperty, box (brush PaletteKey.Accent)
                Button.BorderThicknessProperty, thick Thickness (manager.Metric Metric.BorderWidth) ]

          // 4. 原生 TextBox 与 TextArea
          // 输入类控件边界描边接 ControlStroke（WCAG 1.4.11 ≥ 3:1）
          yield style (fun s -> s.OfType<TextBox>())
              [ TextBox.BackgroundProperty, box (brush PaletteKey.Surface)
                TextBox.ForegroundProperty, box (brush PaletteKey.Text)
                TextBox.BorderBrushProperty, box (brush PaletteKey.ControlStroke)
                TextBox.BorderThicknessProperty, thin ()
                TextBox.CornerRadiusProperty, corner Space.RadiusMd
                TextBox.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Metric Metric.FieldInsetX) (manager.Metric Metric.TextFieldPaddingY))
                Layoutable.MinHeightProperty, (metric Metric.TextFieldTextMinHeight)
                TextBox.FontSizeProperty, (metric Metric.FontBody) ]
          yield style (fun s -> s.OfType<TextBox>().Class(":pointerover"))
              [ TextBox.BorderBrushProperty, box (brush PaletteKey.Line) ]
          yield style (fun s -> s.OfType<TextBox>().Class(":focus"))
              [ TextBox.BorderBrushProperty, box (brush PaletteKey.Accent) ]
          yield style (fun s -> s.OfType<TextBox>().Class(":disabled"))
              [ Visual.OpacityProperty, (metric Metric.OpacityDisabled) ]

          // TextArea 类名扩展
          yield style (fun s -> s.OfType<TextBox>().Class(StyleClass.textArea))
              [ TextBox.AcceptsReturnProperty, box true
                TextBox.TextWrappingProperty, box TextWrapping.Wrap
                Layoutable.MinHeightProperty, (metric Metric.ComposerInputMinHeight) ]
          // CodeBox 等宽文本框
          yield style (fun s -> s.OfType<TextBox>().Class(StyleClass.codeBox))
              [ TextBox.FontFamilyProperty, box manager.MonoFontFamily.Value ]

          // 5. 原生 ToggleSwitch
          yield style (fun s -> s.OfType<ToggleSwitch>())
              [ ToggleSwitch.ForegroundProperty, box (brush PaletteKey.Text)
                ToggleSwitch.FontSizeProperty, (metric Metric.FontBody) ]

          // 6. 原生 ComboBox
          yield style (fun s -> s.OfType<ComboBox>())
              [ ComboBox.BackgroundProperty, box (brush PaletteKey.Surface)
                ComboBox.ForegroundProperty, box (brush PaletteKey.Text)
                ComboBox.BorderBrushProperty, box (brush PaletteKey.ControlStroke)
                ComboBox.BorderThicknessProperty, thin ()
                ComboBox.CornerRadiusProperty, corner Space.RadiusMd
                ComboBox.PaddingProperty, bind (Live.map2 (fun x y -> Thickness(x, y)) (manager.Metric Metric.FieldInsetX) (manager.Metric Metric.TextFieldPaddingY))
                Layoutable.MinHeightProperty, (metric Metric.TextFieldTextMinHeight) ]
          yield style (fun s -> s.OfType<ComboBox>().Class(":pointerover"))
              [ ComboBox.BorderBrushProperty, box (brush PaletteKey.Line) ]
          yield style (fun s -> s.OfType<ComboBox>().Class(":focus-visible"))
              [ ComboBox.BorderBrushProperty, box (brush PaletteKey.Accent) ]
          yield style (fun s -> s.OfType<ComboBox>().Class(":disabled"))
              [ Visual.OpacityProperty, (metric Metric.OpacityDisabled) ]

          // 7. 原生 FlyoutPresenter / MenuFlyoutPresenter
          yield style (fun s -> s.OfType<FlyoutPresenter>())
              [ FlyoutPresenter.BackgroundProperty, box (brush PaletteKey.SurfaceRaised)
                FlyoutPresenter.BorderBrushProperty, box (brush PaletteKey.Border)
                FlyoutPresenter.BorderThicknessProperty, thin ()
                FlyoutPresenter.CornerRadiusProperty, corner Space.RadiusLg ]
          yield style (fun s -> s.OfType<MenuFlyoutPresenter>())
              [ MenuFlyoutPresenter.BackgroundProperty, box (brush PaletteKey.SurfaceRaised)
                MenuFlyoutPresenter.BorderBrushProperty, box (brush PaletteKey.Border)
                MenuFlyoutPresenter.BorderThicknessProperty, thin ()
                MenuFlyoutPresenter.CornerRadiusProperty, corner Space.RadiusLg ]

          // 8. 声明式动效（Animation / Transitions 资源与类名挂接）
          // 呼吸动效（anim-breathe）：不透明度在 min 与 max 之间循环
          let breatheAnim = Avalonia.Animation.Animation()
          breatheAnim.Duration <- manager.Motion.Duration (manager.Timing Timing.CaretBreathPhase).Value
          breatheAnim.IterationCount <- Avalonia.Animation.IterationCount.Infinite
          let bkf0 = Avalonia.Animation.KeyFrame(Cue = Avalonia.Animation.Cue 0.0)
          bkf0.Setters.Add(Setter(Visual.OpacityProperty, (manager.Metric Metric.OpacityCaretBreathMin).Value))
          let bkf1 = Avalonia.Animation.KeyFrame(Cue = Avalonia.Animation.Cue 0.5)
          bkf1.Setters.Add(Setter(Visual.OpacityProperty, (manager.Metric Metric.OpacityCaretBreathMax).Value))
          let bkf2 = Avalonia.Animation.KeyFrame(Cue = Avalonia.Animation.Cue 1.0)
          bkf2.Setters.Add(Setter(Visual.OpacityProperty, (manager.Metric Metric.OpacityCaretBreathMin).Value))
          breatheAnim.Children.Add bkf0
          breatheAnim.Children.Add bkf1
          breatheAnim.Children.Add bkf2
          let breatheStyle = Style(fun s -> s.OfType<Control>().Class(StyleClass.animBreathe))
          breatheStyle.Animations.Add breatheAnim
          yield breatheStyle :> IStyle

          // 旋转动效（anim-spin）：旋转 360 度循环
          let spinAnim = Avalonia.Animation.Animation()
          spinAnim.Duration <- manager.Motion.Duration (manager.Timing Timing.BusySpinnerFrame).Value * 20.0
          spinAnim.IterationCount <- Avalonia.Animation.IterationCount.Infinite
          spinAnim.Easing <- Avalonia.Animation.Easings.LinearEasing()
          let skf0 = Avalonia.Animation.KeyFrame(Cue = Avalonia.Animation.Cue 0.0)
          skf0.Setters.Add(Setter(RotateTransform.AngleProperty, 0.0))
          let skf1 = Avalonia.Animation.KeyFrame(Cue = Avalonia.Animation.Cue 1.0)
          skf1.Setters.Add(Setter(RotateTransform.AngleProperty, 360.0))
          spinAnim.Children.Add skf0
          spinAnim.Children.Add skf1
          let spinStyle = Style(fun s -> s.OfType<Control>().Class(StyleClass.animSpin))
          spinStyle.Setters.Add(Setter(Visual.RenderTransformProperty, RotateTransform()))
          spinStyle.Animations.Add spinAnim
          yield spinStyle :> IStyle

          // 减弱动效切换时，动态更新动效时长为 Zero
          manager.Motion.Changed.Add(fun reduced ->
              if reduced then
                  breatheAnim.Duration <- TimeSpan.Zero
                  spinAnim.Duration <- TimeSpan.Zero
              else
                  breatheAnim.Duration <- TimeSpan.FromMilliseconds (manager.Timing Timing.CaretBreathPhase).Value
                  spinAnim.Duration <- TimeSpan.FromMilliseconds ((manager.Timing Timing.BusySpinnerFrame).Value * 20.0)) ]

    do for s in build () do group.Add s

    /// 本对象拥有的样式组
    /// 创建原生 Button / ToggleButton 的 ControlTheme（若外部需要挂载到资源字典）
    member _.CreateButtonTheme() : ControlTheme =
        let theme = ControlTheme(typeof<Button>)
        let template = FuncControlTemplate<Button>(fun (parent: Button) (scope: INameScope) ->
            let border = Border()
            border.Name <- "PART_Border"
            border.Bind(Border.BackgroundProperty, Avalonia.Data.Binding("Background", Source = parent)) |> ignore
            border.Bind(Border.BorderBrushProperty, Avalonia.Data.Binding("BorderBrush", Source = parent)) |> ignore
            border.Bind(Border.BorderThicknessProperty, Avalonia.Data.Binding("BorderThickness", Source = parent)) |> ignore
            border.Bind(Border.CornerRadiusProperty, Avalonia.Data.Binding("CornerRadius", Source = parent)) |> ignore
            border.Bind(Border.PaddingProperty, Avalonia.Data.Binding("Padding", Source = parent)) |> ignore
            let cp = ContentPresenter()
            cp.Name <- "PART_ContentPresenter"
            cp.Bind(ContentPresenter.ContentProperty, Avalonia.Data.Binding("Content", Source = parent)) |> ignore
            cp.Bind(ContentPresenter.ContentTemplateProperty, Avalonia.Data.Binding("ContentTemplate", Source = parent)) |> ignore
            cp.Bind(ContentPresenter.HorizontalContentAlignmentProperty, Avalonia.Data.Binding("HorizontalContentAlignment", Source = parent)) |> ignore
            cp.Bind(ContentPresenter.VerticalContentAlignmentProperty, Avalonia.Data.Binding("VerticalContentAlignment", Source = parent)) |> ignore
            border.Child <- cp
            border :> Control
        )
        theme.Setters.Add(Setter(Button.TemplateProperty, template))
        theme.Setters.Add(Setter(Button.FocusAdornerProperty, null))
        theme.Setters.Add(Setter(Button.HorizontalAlignmentProperty, HorizontalAlignment.Left))
        theme.Setters.Add(Setter(Button.VerticalAlignmentProperty, VerticalAlignment.Center))
        theme

    /// 创建原生 ToggleButton 的 ControlTheme
    member this.CreateToggleButtonTheme() : ControlTheme =
        let theme = ControlTheme(typeof<ToggleButton>)
        theme.BasedOn <- this.CreateButtonTheme()
        theme

    /// 安装 ControlThemes 到指定的 ResourceDictionary（供原生控件通过资源层获得无焦点框的模板）
    member this.InstallControlThemes(dict: IResourceDictionary) =
        dict[box typeof<Button>] <- box (this.CreateButtonTheme())
        dict[box typeof<ToggleButton>] <- box (this.CreateToggleButtonTheme())

    /// 一次性安装入口：把资源字典与全部样式安装到目标 Application 或 TopLevel/Window
    member this.InstallAll(appOrTopLevel: obj) =
        match appOrTopLevel with
        | :? Application as app ->
            if not (app.Resources.MergedDictionaries.Contains (manager.Resources :> IResourceDictionary)) then
                app.Resources.MergedDictionaries.Add (manager.Resources :> IResourceDictionary)
            this.InstallControlThemes app.Resources
            this.Install app.Styles
        | :? Window as win ->
            if not (win.Resources.MergedDictionaries.Contains (manager.Resources :> IResourceDictionary)) then
                win.Resources.MergedDictionaries.Add (manager.Resources :> IResourceDictionary)
            this.InstallControlThemes win.Resources
            this.Install win.Styles
        | :? TopLevel as tl ->
            if not (tl.Resources.MergedDictionaries.Contains (manager.Resources :> IResourceDictionary)) then
                tl.Resources.MergedDictionaries.Add manager.Resources
            this.InstallControlThemes tl.Resources
            this.Install tl.Styles
        | :? Styles as st ->
            this.Install st
        | _ ->
            ()

    member _.Styles = group

    /// 装进应用或窗口的 Styles；同一目标装多次无害
    member _.Install(target: Styles) = if not (target.Contains group) then target.Add group

    /// 让一个表面参与统一状态反馈：挂 surface 与悬停变体，并挂状态过渡（只补间颜色）
    member _.Surface(control: Control, variant: string) =
        match control with
        | :? Border as b ->
            b.Classes.Add StyleClass.surface
            if not (System.String.IsNullOrEmpty variant) then b.Classes.Add variant
            b.Transitions <- manager.StateTransitions((variant = StyleClass.hoverBordered))
        | :? Button as btn ->
            btn.Classes.Add StyleClass.surface
            if not (System.String.IsNullOrEmpty variant) then btn.Classes.Add variant
            btn.Transitions <- manager.StateTransitions((variant = StyleClass.hoverBordered))
        | _ ->
            control.Classes.Add StyleClass.surface
            if not (System.String.IsNullOrEmpty variant) then control.Classes.Add variant
            control.Transitions <- manager.StateTransitions((variant = StyleClass.hoverBordered))
