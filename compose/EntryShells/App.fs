namespace Wanxiang.UI

open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Controls.Primitives
open Avalonia.Media
open Avalonia.Styling
open Avalonia.Themes.Fluent
open Wanxiang2.ThemePalette

/// 决策 261：输入框右键 / 长按菜单（Fluent 的 TextBox ContextFlyout）默认是英文
/// Cut / Copy / Paste——r9 走查在手机 PWA 上截到。Fluent 的文案走资源键，这里统一改成中文。
module TextFlyoutLocale =
    let strings =
        [ "StringTextFlyoutCutText", "剪切"
          "StringTextFlyoutCopyText", "复制"
          "StringTextFlyoutPasteText", "粘贴" ]

    let apply (resources: IResourceDictionary) =
        for key, text in strings do
            resources[key] <- text

/// 决策 276：触屏时隐藏菜单项右侧的快捷键文字（Fluent MenuItem 模板部件 PART_InputGestureText）。
module TouchMenuGestures =
    let style () =
        let s = Style(fun selector -> selector.OfType<MenuItem>().Template().OfType<TextBlock>().Name "PART_InputGestureText")
        s.Setters.Add(Setter(Visual.IsVisibleProperty, false))
        s

/// 万象应用（迁自宿主 src/Wanxiang.UI/App.fs；clean-room：仅以原项目为灵感，UI 独立设计）。
///
/// 视觉体系完全由 wanxiang2 的 `Theme`（ThemeManager / ThemeStyles）决定，Fluent 只提供控件模板。
/// 这里做三件事：把 Fluent 的强调色与选中色压成万象自己的、把 TextBox 模板那圈默认描边彻底去掉、
/// 以及把弹出层（ToolTip / ContextMenu / MenuFlyoutPresenter）的字体与底色改成万象的活画笔。
/// 根视图（MainView）由 AppShell 组合者提供，经静态注入端口接入：桌面分支组装 MainWindow，
/// PWA 分支直接挂 SingleView。本壳不持 DispatcherTimer、不做文件 IO。
type App() =
    inherit Application()

    // ---- 注入端口（桌面 / PWA 入口在 AppBuilder 启动前设置；MainView 由 AppShell 组合者提供）----

    /// 根视图工厂（MainView 创建；桌面与 PWA 共用，决策 48）。
    static member val Root: (unit -> Control) option = None with get, set
    /// 视图构建（AppShell.Build）。
    static member val Build: (Control -> unit) option = None with get, set
    /// 关窗落草稿（AppShell.FlushDrafts）。
    static member val FlushDrafts: (unit -> unit) option = None with get, set
    /// 读本机偏好（文件 IO，由入口注入）。
    static member val LoadPrefs: (unit -> UiPrefs) option = None with get, set
    /// 写本机偏好（文件 IO，由入口注入）。
    static member val SavePrefs: (UiPrefs -> unit) option = None with get, set
    /// 采样模式首帧打点（可选；进程退出等副作用由入口注入）。
    static member val MarkFirstFrame: (unit -> unit) option = None with get, set

    /// Fluent 的 TextBox 在各状态下用主题资源画自己的底色和描边，
    /// 与万象「外壳负责描边、输入框自身透明」的结构冲突。
    /// 压模板 Border 只在名字对得上时有效，因此这里同时改写资源键——
    /// 少了这一步，输入框会在浅纸背景上呈现一块更深的灰板。
    member private this.FlattenTextBoxChrome() =
        let transparent = Brushes.Transparent :> IBrush
        for key in
            [ "TextControlBackground"
              "TextControlBackgroundPointerOver"
              "TextControlBackgroundFocused"
              "TextControlBackgroundDisabled"
              "TextControlBorderBrush"
              "TextControlBorderBrushPointerOver"
              "TextControlBorderBrushFocused"
              "TextControlBorderBrushDisabled" ] do
            this.Resources[key] <- transparent
        this.Resources["TextControlBorderThemeThickness"] <- Thickness 0.0
        this.Resources["TextControlBorderThemeThicknessFocused"] <- Thickness 0.0
        this.Resources["TextControlThemePadding"] <- Thickness 0.0
        this.Resources["TextControlForeground"] <- SharedTheme.manager.Brush PaletteKey.Text
        this.Resources["TextControlForegroundPointerOver"] <- SharedTheme.manager.Brush PaletteKey.Text
        this.Resources["TextControlForegroundFocused"] <- SharedTheme.manager.Brush PaletteKey.Text
        this.Resources["TextControlPlaceholderForeground"] <- SharedTheme.manager.Brush PaletteKey.TextFaint
        this.Resources["TextControlPlaceholderForegroundPointerOver"] <- SharedTheme.manager.Brush PaletteKey.TextFaint
        this.Resources["TextControlPlaceholderForegroundFocused"] <- SharedTheme.manager.Brush PaletteKey.TextFaint
        this.Resources["TextControlSelectionHighlightColor"] <- SharedTheme.manager.Brush PaletteKey.AccentSoft
        for pseudo in [ ""; ":focus"; ":pointerover"; ":disabled" ] do
            let style =
                Style(fun selector ->
                    let baseSelector = selector.OfType<TextBox>()
                    let stated = if pseudo = "" then baseSelector else baseSelector.Class pseudo
                    stated.Template().OfType<Border>().Name "PART_BorderElement")
            style.Setters.Add(Setter(Border.BorderThicknessProperty, Thickness 0.0))
            style.Setters.Add(Setter(Border.BorderBrushProperty, transparent))
            style.Setters.Add(Setter(Border.BackgroundProperty, transparent))
            this.Styles.Add style

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        // 共享类选择器样式（Wanxiang2.Theme.ThemeStyles），绑定 SharedTheme 的活实例，只装一次；
        // 交互状态（:focus-visible / :pointerover / :pressed / :disabled）也由它提供（旧 Interaction.register）。
        SharedTheme.styles.Install this.Styles
        SharedTheme.styles.InstallControlThemes this.Resources
        // 主题资源字典（Palette.* / Metric.* / Space.* / 派生档）装进应用资源：
        // 控件的 DynamicResource 声明式绑定沿可视树查到 Application 才吃得值（W7 外观退役的通路）。
        if not (this.Resources.MergedDictionaries.Contains (SharedTheme.manager.Resources :> Avalonia.Controls.IResourceDictionary)) then
            this.Resources.MergedDictionaries.Add (SharedTheme.manager.Resources :> Avalonia.Controls.IResourceDictionary)
        Wanxiang2.ImeShield.ImeShield.install ()
        // 画布主题固定为浅色变体：万象自己的深色由 Tokens 提供，
        // 若让 Fluent 也切深色，两套配色会互相打架。
        this.RequestedThemeVariant <- ThemeVariant.Light

        this.Resources["SystemAccentColor"] <- ThemePalette.color PaletteMode.Light PaletteKey.Accent
        this.Resources["SystemAccentColorDark1"] <- ThemePalette.color PaletteMode.Light PaletteKey.AccentHover
        this.Resources["SystemAccentColorLight1"] <- ThemePalette.color PaletteMode.Light PaletteKey.AccentSoft

        this.FlattenTextBoxChrome()

        // 弹出层（ToolTip 等）不在 AppShell 的可视树下，继承不到 AppShell.FontFamily；
        // browser-wasm 又拿不到系统字体，中文提示会渲染成豆腐块（视觉走查：
        // 「Enter □□ / Shift+Enter □□」）。这里给 ToolTip 显式指定内嵌字体。
        let toolTipFont = Style(fun selector -> selector.OfType<ToolTip>())
        toolTipFont.Setters.Add(Setter(TemplatedControl.FontFamilyProperty, SharedTheme.manager.FontFamily.Value))
        this.Styles.Add toolTipFont
        // 右键菜单同理：ContextMenu 是弹出层，PWA 里菜单文字曾整排渲染成豆腐块。
        for popupFont in
            [ Style(fun selector -> selector.OfType<ContextMenu>())
              Style(fun selector -> selector.OfType<MenuItem>()) ] do
            popupFont.Setters.Add(Setter(TemplatedControl.FontFamilyProperty, SharedTheme.manager.FontFamily.Value))
            this.Styles.Add popupFont

        TextFlyoutLocale.apply this.Resources
        // 输入框菜单是 MenuFlyoutPresenter（不是 ContextMenu），同样拿不到内嵌字体。
        let flyoutFont = Style(fun selector -> selector.OfType<MenuFlyoutPresenter>())
        flyoutFont.Setters.Add(Setter(TemplatedControl.FontFamilyProperty, SharedTheme.manager.FontFamily.Value))
        this.Styles.Add flyoutFont
        // 决策 276：画布固定浅色变体，输入框菜单因此在深色主题下仍是一块白板。
        // 菜单底色 / 描边 / 文字改走万象的实时画笔，随主题补间一起变。
        let flyoutChrome = Style(fun selector -> selector.OfType<MenuFlyoutPresenter>())
        flyoutChrome.Setters.Add(Setter(TemplatedControl.BackgroundProperty, SharedTheme.manager.Brush PaletteKey.SurfaceRaised))
        flyoutChrome.Setters.Add(Setter(TemplatedControl.BorderBrushProperty, SharedTheme.manager.Brush PaletteKey.Border))
        flyoutChrome.Setters.Add(Setter(TemplatedControl.ForegroundProperty, SharedTheme.manager.Brush PaletteKey.Text))
        this.Styles.Add flyoutChrome
        let flyoutItem = Style(fun selector -> selector.OfType<MenuFlyoutPresenter>().Descendant().OfType<MenuItem>())
        flyoutItem.Setters.Add(Setter(TemplatedControl.ForegroundProperty, SharedTheme.manager.Brush PaletteKey.Text))
        this.Styles.Add flyoutItem
        for key, brush in
            [ "MenuFlyoutPresenterBackground", SharedTheme.manager.Brush PaletteKey.SurfaceRaised
              "MenuFlyoutPresenterBorderBrush", SharedTheme.manager.Brush PaletteKey.Border
              "MenuFlyoutItemForeground", SharedTheme.manager.Brush PaletteKey.Text
              "MenuFlyoutItemForegroundPointerOver", SharedTheme.manager.Brush PaletteKey.Text
              "MenuFlyoutItemBackgroundPointerOver", SharedTheme.manager.Brush PaletteKey.Hover
              "MenuFlyoutItemBackgroundPressed", SharedTheme.manager.Brush PaletteKey.Pressed
              "MenuFlyoutItemKeyboardAcceleratorTextForeground", SharedTheme.manager.Brush PaletteKey.TextMuted ] do
            this.Resources[key] <- brush
        // 决策 276：触屏上没有键盘，菜单里的「Ctrl+X」提示只是噪声——按输入方式挂 / 摘一条样式。
        let hideGestures = TouchMenuGestures.style ()
        let syncGestures (touch: bool) =
            let present = this.Styles.Contains hideGestures
            if touch && not present then this.Styles.Add hideGestures
            elif not touch && present then this.Styles.Remove hideGestures |> ignore
        syncGestures (SharedTheme.input.PrefersTouch)
        SharedTheme.input.Changed.Add syncGestures

        // 选中/悬停一律走万象的强调浅底与暖灰，不用 Fluent 的糖果蓝
        this.Resources["SystemControlHighlightListAccentLowBrush"] <- SharedTheme.manager.Brush PaletteKey.AccentSoft
        this.Resources["SystemControlHighlightListAccentMediumBrush"] <- SharedTheme.manager.Brush PaletteKey.AccentSoft
        this.Resources["SystemControlHighlightListAccentHighBrush"] <- SharedTheme.manager.Brush PaletteKey.AccentSoft
        this.Resources["SystemControlHighlightListLowBrush"] <- SharedTheme.manager.Brush PaletteKey.Hover
        this.Resources["SystemControlHighlightListMediumBrush"] <- SharedTheme.manager.Brush PaletteKey.Pressed

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            // 桌面：窗口壳（MainWindow）由本组件提供，根视图与 IO 经注入端口接入。
            // 没有注入 LoadPrefs / SavePrefs 就不创建窗口（壳不硬编码设计层数值，也不假装能做持久化）。
            match App.Root, App.LoadPrefs, App.SavePrefs with
            | Some root, Some loadPrefs, Some savePrefs ->
                let port =
                    { Root = root
                      Build = App.Build |> Option.defaultValue ignore
                      FlushDrafts = App.FlushDrafts |> Option.defaultValue ignore
                      LoadPrefs = loadPrefs
                      SavePrefs = savePrefs
                      MarkFirstFrame = App.MarkFirstFrame }
                desktop.MainWindow <- MainWindow(port)
            | _ -> ()
        | :? ISingleViewApplicationLifetime as singleView ->
            // PWA：browser 没有 Window，根视图必须是 Control（决策 48）
            match App.Root with
            | Some root ->
                let view = root ()
                singleView.MainView <- view
                App.Build |> Option.iter (fun b -> b view)
            | None -> ()
        | _ -> ()
        base.OnFrameworkInitializationCompleted()
