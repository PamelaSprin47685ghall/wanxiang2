namespace Wanxiang2.Theme

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading
open Wanxiang.UI
open Wanxiang2.ThemePalette
open Wanxiang2.ThemeMetrics
open Wanxiang2.MotionSwitch
open Wanxiang2.ThemeFonts

/// 由两支基础画笔叠出来的悬停底色（旧 Interaction 的 blendOver）。
/// 旧代码在注册样式时叠一次就定死；这里每次基础画笔改色都重算，因此跟随主题。
[<RequireQualifiedAccess>]
type DerivedBrush =
    /// surface 上叠 hover
    | HoverRaised
    /// dangerSoft 上叠 hover
    | HoverDanger
    /// selected 上叠 hover（选中行悬停）
    | HoverSelected

/// 主题管理者：拥有全部活的样式值，并负责切换主题。
///
/// - 画笔：每个配色项一支 SolidColorBrush，整个生命周期是同一实例；切主题时原地改色（补间归 ThemeTween）。
/// - 尺寸、间距、时长、字体：LiveValue，改值即推送给绑定它的控件与样式。
/// - 帧：默认用线程池计时器按墙钟逐帧投递到 UI 线程（与旧 Tokens 相同，理由见旧注释：无显示测试不泵 DispatcherTimer）；
///   autoFrames = false 时由调用方调 Pump 推进，测试据此精确控制时间。
/// - 代次：每次补间开始加一；过期帧不负责停帧，免得迟到的一帧误停新补间。
/// 只在 UI 线程上调用。
type ThemeManager(clock: unit -> DateTimeOffset, autoFrames: bool) =
    let mutable mode = PaletteMode.Light
    let motion = MotionSwitch()
    let tween = ThemeTween<PaletteKey>()
    let changed = Event<PaletteMode>()
    let brushes = Dictionary<PaletteKey, SolidColorBrush>()

    do
        for k in ThemePalette.keys do
            brushes[k] <- SolidColorBrush(ThemePalette.color PaletteMode.Light k)

    let derived = Dictionary<DerivedBrush, SolidColorBrush>()

    let blend (b: Color) (o: Color) =
        let a, r, g, bl = ColorBlend.over (b.A, b.R, b.G, b.B) (o.A, o.R, o.G, o.B)
        Color.FromArgb(a, r, g, bl)

    let derivedColor d =
        match d with
        | DerivedBrush.HoverRaised -> blend brushes[PaletteKey.Surface].Color brushes[PaletteKey.Hover].Color
        | DerivedBrush.HoverDanger -> blend brushes[PaletteKey.DangerSoft].Color brushes[PaletteKey.Hover].Color
        | DerivedBrush.HoverSelected -> blend brushes[PaletteKey.Selected].Color brushes[PaletteKey.Hover].Color

    do
        for d in
            [
                DerivedBrush.HoverRaised
                DerivedBrush.HoverDanger
                DerivedBrush.HoverSelected
            ] do
            derived[d] <- SolidColorBrush(derivedColor d)

    let refreshDerived () =
        for KeyValue(d, b) in derived do
            b.Color <- derivedColor d

    let metrics = Dictionary<Metric, LiveValue<float>>()

    do
        for m in ThemeMetrics.all do
            metrics[m] <- LiveValue(ThemeMetrics.initial m)

    let spaces = Dictionary<Space, LiveValue<float>>()

    do
        for s in Space.all do
            spaces[s] <- LiveValue(Space.initial s)

    let timings = Dictionary<Timing, LiveValue<float>>()

    do
        for t in MotionTimings.all do
            timings[t] <- LiveValue(MotionTimings.initialMs t)

    let fontFamily = LiveValue(FontFamily ThemeFonts.body)
    let serifFamily = LiveValue(FontFamily ThemeFonts.display)
    let monoFontFamily = LiveValue(FontFamily ThemeFonts.mono)
    let mutable frameTimer: Threading.Timer option = None
    let mutable generation = 0
    let toArgb (c: Color) : Argb = { A = c.A; R = c.R; G = c.G; B = c.B }
    let ofArgb (c: Argb) = Color.FromArgb(c.A, c.R, c.G, c.B)

    let transitionMs () =
        if motion.IsReduced then
            0.0
        else
            timings[Timing.ThemeColorTransition].Value

    let stopFrames () =
        frameTimer |> Option.iter (fun t -> t.Dispose())
        frameTimer <- None

    let snapAll () =
        for KeyValue(k, b) in brushes do
            b.Color <- ThemePalette.color mode k

        refreshDerived ()

    let resources = Controls.ResourceDictionary()

    let initResources () =
        // 1. 活画笔直接放入基础字典，键名按 PaletteKey.<Name>
        for KeyValue(k, b) in brushes do
            let keyName = sprintf "Palette.%A" k
            resources[box keyName] <- box b
            resources[box (sprintf "Brush.%A" k)] <- box b

        for KeyValue(d, b) in derived do
            resources[box (sprintf "Derived.%A" d)] <- box b
        // 2. ThemeDictionaries（Light / Dark）供 Avalonia 原生 ThemeVariant 解析
        let lightDict = Controls.ResourceDictionary()
        let darkDict = Controls.ResourceDictionary()

        for k in ThemePalette.keys do
            let keyName = sprintf "Palette.%A" k
            lightDict[box keyName] <- box (SolidColorBrush(ThemePalette.color PaletteMode.Light k))
            darkDict[box keyName] <- box (SolidColorBrush(ThemePalette.color PaletteMode.Dark k))

        resources.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Light] <-
            (lightDict :> Controls.IThemeVariantProvider)

        resources.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Dark] <- (darkDict :> Controls.IThemeVariantProvider)
        // 3. 度量、间距、字体与时长
        for KeyValue(m, v) in metrics do
            let keyName = sprintf "Metric.%A" m
            resources[box keyName] <- box v.Value

            (v :> IObservable<float>).Subscribe(fun x -> resources[box keyName] <- box x)
            |> ignore

        for KeyValue(s, v) in spaces do
            let keyName = sprintf "Space.%A" s
            resources[box keyName] <- box v.Value

            (v :> IObservable<float>).Subscribe(fun x -> resources[box keyName] <- box x)
            |> ignore

        for KeyValue(t, v) in timings do
            let keyName = sprintf "Timing.%A" t
            resources[box keyName] <- box v.Value

            (v :> IObservable<float>).Subscribe(fun x -> resources[box keyName] <- box x)
            |> ignore

        resources[box "FontFamily.Body"] <- box fontFamily.Value

        (fontFamily :> IObservable<FontFamily>).Subscribe(fun f -> resources[box "FontFamily.Body"] <- box f)
        |> ignore

        resources[box "FontFamily.Serif"] <- box serifFamily.Value

        (serifFamily :> IObservable<FontFamily>).Subscribe(fun f -> resources[box "FontFamily.Serif"] <- box f)
        |> ignore

        resources[box "FontFamily.Mono"] <- box monoFontFamily.Value

        (monoFontFamily :> IObservable<FontFamily>).Subscribe(fun f -> resources[box "FontFamily.Mono"] <- box f)
        |> ignore
        // 减弱动效开关的资源化：控件在动作时刻经 TryFindResource 取值，不再挂 IObservable
        resources[box "Motion.Reduced"] <- box motion.IsReduced

        motion.Changed.Subscribe(fun r -> resources[box "Motion.Reduced"] <- box r)
        |> ignore
        // 常用派生度量（Thickness / CornerRadius）预置
        let updateDerivedMetrics () =
            resources[box "CornerRadius.RadiusXs"] <- box (CornerRadius spaces[Space.RadiusXs].Value)
            resources[box "CornerRadius.RadiusSm"] <- box (CornerRadius spaces[Space.RadiusSm].Value)
            resources[box "CornerRadius.RadiusMd"] <- box (CornerRadius spaces[Space.RadiusMd].Value)
            resources[box "CornerRadius.RadiusLg"] <- box (CornerRadius spaces[Space.RadiusLg].Value)
            resources[box "CornerRadius.RadiusPill"] <- box (CornerRadius spaces[Space.RadiusPill].Value)
            resources[box "Thickness.BorderThin"] <- box (Thickness metrics[Metric.BorderWidth].Value)
            resources[box "Thickness.None"] <- box (Thickness 0.0)
            // 复合几何档：控件用 DynamicResource 键即可吃到「非均匀」的主题几何，
            // 不用再手写 Subscribe 拼 Thickness/CornerRadius（W7 外观退役）。
            resources[box "CornerRadius.QuoteRight"] <-
                box (CornerRadius(0.0, spaces[Space.RadiusSm].Value, spaces[Space.RadiusSm].Value, 0.0))

            resources[box "CornerRadius.ListMarkerDot"] <- box (CornerRadius metrics[Metric.ListMarkerDotRadius].Value)
            resources[box "Thickness.BorderBottom"] <- box (Thickness(0.0, 0.0, 0.0, metrics[Metric.BorderWidth].Value))
            resources[box "Thickness.TaskBoxEdge"] <- box (Thickness metrics[Metric.TaskBoxBorderWidth].Value)
            resources[box "Thickness.QuoteEdge"] <- box (Thickness(metrics[Metric.QuoteEdgeWidth].Value, 0.0, 0.0, 0.0))
            resources[box "Thickness.GapRightMd"] <- box (Thickness(0.0, 0.0, spaces[Space.Md].Value, 0.0))
            resources[box "Margin.GapRightMd"] <- box (Thickness(0.0, 0.0, spaces[Space.Md].Value, 0.0))
            resources[box "Thickness.GapBottomMd"] <- box (Thickness(0.0, 0.0, 0.0, spaces[Space.Md].Value))

            resources[box "Padding.Block"] <-
                box (Thickness(metrics[Metric.BlockPaddingX].Value, metrics[Metric.BlockPaddingY].Value))

            resources[box "Margin.Block"] <-
                box (Thickness(metrics[Metric.BlockPaddingX].Value, metrics[Metric.BlockPaddingY].Value))
            // 覆盖层两档卡：对话框卡与浮层卡的均匀内边距。
            resources[box "Padding.OverlayDialog"] <- box (Thickness(spaces[Space.Xl].Value))
            resources[box "Padding.OverlayPopup"] <- box (Thickness(spaces[Space.Xs].Value))
            // 覆盖层视口留白：对话框卡的外边距，同时也是钳位基准（宽 = 视口 − 2×留白）。
            resources[box "Margin.OverlayInset"] <- box (Thickness(spaces[Space.Xl].Value))
            // 浮层菜单的排版档：项内边距、提示左边距、分隔留白、组标题留白。
            resources[box "Padding.MenuItem"] <- box (Thickness(spaces[Space.Md].Value, spaces[Space.Xs].Value))
            resources[box "Margin.MenuHint"] <- box (Thickness(spaces[Space.Xl].Value, 0.0, 0.0, 0.0))
            resources[box "Margin.MenuDivider"] <- box (Thickness(spaces[Space.Sm].Value, spaces[Space.XXs].Value))

            resources[box "Margin.MenuHeader"] <-
                box (
                    Thickness(
                        spaces[Space.Md].Value,
                        spaces[Space.Sm].Value,
                        spaces[Space.Md].Value,
                        spaces[Space.XXs].Value
                    )
                )

            resources[box "Padding.CodeHeader"] <-
                box (
                    Thickness(
                        metrics[Metric.BlockPaddingX].Value,
                        spaces[Space.Xs].Value,
                        spaces[Space.Xs].Value,
                        spaces[Space.Xs].Value
                    )
                )

            resources[box "Margin.CodeExpand"] <-
                box (
                    Thickness(metrics[Metric.BlockPaddingX].Value, spaces[Space.Xs].Value, 0.0, spaces[Space.Xs].Value)
                )

            resources[box "Padding.Quote"] <-
                box (
                    Thickness(
                        spaces[Space.Md].Value,
                        spaces[Space.Sm].Value,
                        spaces[Space.Md].Value,
                        spaces[Space.Sm].Value
                    )
                )

            resources[box "Margin.QuoteNested"] <-
                box (Thickness(spaces[Space.Md].Value, 0.0, 0.0, Wanxiang.UI.Spacing.spaceMd))

            resources[box "Padding.MathBlock"] <- box (Thickness(0.0, spaces[Space.Md].Value))
            resources[box "Margin.AvatarRight"] <- box (Thickness(0.0, 0.0, spaces[Space.Xl].Value, 0.0))
            resources[box "Margin.MathInline"] <- box (Thickness(spaces[Space.Xs].Value, 0.0))
            resources[box "Margin.FieldError"] <- box (Thickness(0.0, spaces[Space.XXs].Value, 0.0, 0.0))
            resources[box "Margin.GapBottomMd"] <- box (Thickness(0.0, 0.0, 0.0, spaces[Space.Md].Value))
            // 脚注缩进 = 头像宽 + 头像右间距；左右两档与 isUser 对应
            let gutter = metrics[Metric.LogoAvatar].Value + spaces[Space.Xl].Value
            let md = spaces[Space.Md].Value
            resources[box "Margin.AssistantIndent"] <- box (Thickness(gutter, md, 0.0, 0.0))
            resources[box "Margin.UserIndent"] <- box (Thickness(0.0, md, gutter, 0.0))
            // 输入区留白：宽屏顶=Space.Md、底=Space.X2l（react composer-wrap padding-block 8/16）；窄屏固定收紧（左右=Space.Xl、底=Space.Md）。
            // 水平档（窄屏 16 / 中 24 / 桌面 32 / 宽屏 48）随自身宽度取，不在主题里（见 LayoutPolicy.horizontalInset）。
            resources[box "Margin.Composer"] <-
                box (Thickness(0.0, spaces[Space.Md].Value, 0.0, spaces[Space.X2l].Value))

            // 阅读列尾部呼吸留白（react thread 的 padding-bottom 48）：只取底档，随 MessageEndBreathing 活值重算。
            resources[box "Margin.MessageEnd"] <-
                box (Thickness(0.0, 0.0, 0.0, metrics[Metric.MessageEndBreathing].Value))

            resources[box "Margin.ComposerCompact"] <-
                box (Thickness(spaces[Space.Xl].Value, spaces[Space.Xl].Value, 0.0, spaces[Space.Md].Value))
            // 强调字距档（正文语言标签等）：静态常量，来自 SpacingRamp
            resources[box "Tracking.Emphasis"] <- box Wanxiang.UI.Spacing.Tracking.emphasis

        updateDerivedMetrics ()
        // 派生档的唯一刷新点：任何来源活值改动都重算全部复合几何（幂等、开销为常数）。
        let refresh () = updateDerivedMetrics ()

        for m in
            [
                Metric.BorderWidth
                Metric.ListMarkerDotRadius
                Metric.TaskBoxBorderWidth
                Metric.QuoteEdgeWidth
                Metric.BlockPaddingX
                Metric.BlockPaddingY
                Metric.LogoAvatar
                Metric.MessageEndBreathing
            ] do
            (metrics[m] :> IObservable<float>).Subscribe(fun _ -> refresh ()) |> ignore

        for s in
            [
                Space.RadiusXs
                Space.RadiusMd
                Space.RadiusLg
                Space.RadiusPill
                Space.RadiusSm
                Space.Md
                Space.Sm
                Space.Xs
                Space.XXs
                Space.Xl
                Space.X2l
            ] do
            (spaces[s] :> IObservable<float>).Subscribe(fun _ -> refresh ()) |> ignore

    do initResources ()

    new() = new ThemeManager((fun () -> DateTimeOffset.UtcNow), true)

    member _.Mode = mode
    member _.Motion = motion
    /// 补间代次
    member _.Generation = generation
    member _.IsTransitioning = tween.IsRunning

    /// 主题变了（模式与调色板已更新，画笔可能还在补间）
    [<CLIEvent>]
    member _.Changed = changed.Publish

    /// 某配色项的活画笔（同一项永远是同一实例）
    member _.Brush(key: PaletteKey) : SolidColorBrush = brushes[key]
    member _.Derived(d: DerivedBrush) : SolidColorBrush = derived[d]
    /// 当前模式下某项的目标色（非画笔量，如阴影色，按它取）
    member _.Color(key: PaletteKey) = ThemePalette.color mode key

    member _.Metric(m: Metric) : LiveValue<float> = metrics[m]
    member _.Space(s: Space) : LiveValue<float> = spaces[s]
    member _.Timing(t: Timing) : LiveValue<float> = timings[t]
    member _.FontFamily = fontFamily
    member _.SerifFontFamily = serifFamily
    member _.Resources = resources
    member _.MergedDictionary = resources
    member _.MonoFontFamily = monoFontFamily

    member _.ApplyMetric(m: Metric, v: float) = metrics[m].Set v
    member _.ApplySpace(s: Space, v: float) = spaces[s].Set v

    /// 推进一帧（帧计时器调；autoFrames = false 时由调用方调）。forGeneration 不是当前代次时只画不停帧
    member this.Pump(?forGeneration: int) =
        let g = defaultArg forGeneration generation
        let frame = tween.Frame(clock (), transitionMs ())

        for k, c in frame.Colors do
            brushes[k].Color <- ofArgb c

        if not frame.Colors.IsEmpty then
            refreshDerived ()

        if frame.Finished && g = generation then
            stopFrames ()

    /// 切换主题：画笔从当前色补间到新色；补间途中再切换时后来者接管。减弱动效或同主题重放同步到位
    member this.Apply(next: PaletteMode) =
        let sameMode = (mode = next)
        mode <- next

        let plan =
            [
                for KeyValue(k, b) in brushes -> k, toArgb b.Color, toArgb (ThemePalette.color next k)
            ]

        match tween.Apply(clock (), sameMode, transitionMs (), plan) with
        | ThemeStart.Snap ->
            stopFrames ()
            snapAll ()
        | ThemeStart.Tween ->
            generation <- generation + 1
            stopFrames ()

            if autoFrames then
                let g = generation

                let period =
                    TimeSpan.FromTicks(max 1L (TimeSpan.FromMilliseconds(transitionMs ()).Ticks / 12L))

                frameTimer <-
                    Some(
                        new Threading.Timer(
                            (fun _ -> Dispatcher.UIThread.InvokeAsync(fun () -> this.Pump g) |> ignore),
                            null,
                            period,
                            period
                        )
                    )

        changed.Trigger next

    /// 立即收尾到目标色并停帧（旧 completeTransition）
    member _.CompleteTransition() =
        stopFrames ()
        generation <- generation + 1
        tween.Complete() |> ignore
        snapAll ()

    /// 中等抬升阴影：下拉、气泡（旧 Tokens.shadowPopup）
    member this.ShadowPopup() =
        BoxShadows(
            BoxShadow(
                OffsetX = 0.0,
                OffsetY = 8.0,
                Blur = 26.0,
                Spread = -8.0,
                Color = this.Color PaletteKey.ShadowStrong
            )
        )

    /// 对话框阴影（旧 Tokens.shadowDialog）
    member this.ShadowDialog() =
        BoxShadows(
            BoxShadow(
                OffsetX = 0.0,
                OffsetY = 18.0,
                Blur = 48.0,
                Spread = -12.0,
                Color = this.Color PaletteKey.ShadowStrong
            )
        )

    /// 状态过渡：只补间底色（bordered = 另补间描边，只给描边即状态的表面）。时长经减弱动效门控
    member _.StateTransitions(bordered: bool) : Animation.Transitions =
        let duration = motion.Duration timings[Timing.ControlStateTransition].Value

        let make (p: AvaloniaProperty) =
            let t = Animation.BrushTransition()
            t.Property <- p
            t.Duration <- duration
            t.Easing <- EaseOutCubicEasing()
            t

        let ts = Animation.Transitions()
        ts.Add(make Controls.Border.BackgroundProperty)

        if bordered then
            ts.Add(make Controls.Border.BorderBrushProperty)

        ts

    interface IDisposable with
        member _.Dispose() = stopFrames ()
