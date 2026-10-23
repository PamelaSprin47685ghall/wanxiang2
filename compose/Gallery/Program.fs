module Wanxiang2.Gallery.Program

open System
open System.IO
open System.Threading
open Avalonia
open Avalonia.Automation
open Avalonia.Automation.Peers
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Headless
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Styling
open Avalonia.Themes.Fluent
open Wanxiang2.ThemePalette

/// 画廊的桌面应用（交互窗口用）：与 headless 应用同一个主题与样式，只是不画到内存位图。
type GalleryDesktopApp() =
    inherit Application()

    /// 窗口构造完之后的回调（AppShell 式的注入端口）。
    static member val OnReady: (unit -> unit) option = None with get, set

    /// 构造好的主窗口（由 OnReady 填）。
    static member val Window: Window option = None with get, set

    override this.Initialize() =
        this.Styles.Add(FluentTheme())
        // 主题资源字典与共享样式：与真实宿主（compose/EntryShells/App.fs）同一套挂法。
        let theme =
            new Wanxiang2.Theme.ThemeManager((fun () -> DateTimeOffset.UtcNow), true)

        let styles = new Wanxiang2.Theme.ThemeStyles(theme)
        styles.Install this.Styles
        styles.InstallControlThemes this.Resources

        if not (this.Resources.MergedDictionaries.Contains(theme.Resources :> IResourceDictionary)) then
            this.Resources.MergedDictionaries.Add(theme.Resources :> IResourceDictionary)

        this.RequestedThemeVariant <- ThemeVariant.Light

    override this.OnFrameworkInitializationCompleted() =
        match this.ApplicationLifetime with
        | :? IClassicDesktopStyleApplicationLifetime as desktop ->
            match GalleryDesktopApp.OnReady with
            | Some ready ->
                ready ()

                match GalleryDesktopApp.Window with
                | Some w -> desktop.MainWindow <- w
                | None -> ()
            | None -> ()
        | _ -> ()

        base.OnFrameworkInitializationCompleted()

/// 画廊的 headless 应用：不连主机、不读本机文件，只为把控件画出来。
type GalleryApp() =
    inherit Application()
    override this.Initialize() = this.Styles.Add(FluentTheme())

type GalleryAppBuilder =
    static member BuildAvaloniaApp() =
        // 必须 UseSkia + UseHeadlessDrawing = false：headless 的默认实现不画，
        // RenderTargetBitmap 会得到一张空图（figuregold 同一手法）。
        AppBuilder
            .Configure<GalleryApp>()
            .UseSkia()
            .UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

/// 把控件渲染成 PNG。
module Render =
    /// 渲染一个控件到 PNG 文件。返回写出的字节数。
    let toPng (control: Control) (path: string) : int =
        let w = max 1 (int (Math.Ceiling control.Bounds.Width))
        let h = max 1 (int (Math.Ceiling control.Bounds.Height))
        use bmp = new RenderTargetBitmap(PixelSize(w, h), Vector(96.0, 96.0))
        bmp.Render control
        let dir = Path.GetDirectoryName path

        if not (String.IsNullOrEmpty dir) && not (Directory.Exists dir) then
            Directory.CreateDirectory dir |> ignore

        bmp.Save(path, (PngBitmapEncoderOptions()) :> BitmapEncoderOptions)
        FileInfo(path).Length |> int

    /// 一张图里有多少个不同的像素值（判「不是纯色板」用）。
    let distinctPixels (control: Control) : int =
        let w = max 1 (int (Math.Ceiling control.Bounds.Width))
        let h = max 1 (int (Math.Ceiling control.Bounds.Height))
        use bmp = new RenderTargetBitmap(PixelSize(w, h), Vector(96.0, 96.0))
        bmp.Render control
        let stride = w * 4
        let buffer = Array.zeroCreate<byte>(stride * h)

        let handle =
            Runtime.InteropServices.GCHandle.Alloc(buffer, Runtime.InteropServices.GCHandleType.Pinned)

        try
            bmp.CopyPixels(PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride)
        finally
            handle.Free()

        let seen = System.Collections.Generic.HashSet<uint32>()

        for i in 0 .. (buffer.Length / 4) - 1 do
            let v =
                uint32 buffer.[i * 4]
                ||| (uint32 buffer.[i * 4 + 1] <<< 8)
                ||| (uint32 buffer.[i * 4 + 2] <<< 16)
                ||| (uint32 buffer.[i * 4 + 3] <<< 24)

            seen.Add v |> ignore

        seen.Count

module Program =

    let private usage () =
        eprintfn "用法："
        eprintfn "  dotnet run --project compose/Gallery/Gallery.fsproj -- --list"

        eprintfn
            "  dotnet run --project compose/Gallery/Gallery.fsproj -- --render <条目名> <输出.png> [--theme <light|dark>]"

        eprintfn
            "  dotnet run --project compose/Gallery/Gallery.fsproj -- --check-baselines <基线目录> [--theme <light|dark|both>]"

        eprintfn
            "  dotnet run --project compose/Gallery/Gallery.fsproj -- --generate-baselines <输出目录> [--theme <light|dark|both>]"

        eprintfn "  dotnet run --project compose/Gallery/Gallery.fsproj -- --a11y-audit"
        eprintfn "  dotnet run --project compose/Gallery/Gallery.fsproj -- --window"
        eprintfn ""
        eprintfn "条目："

        for e in Gallery.entries do
            eprintfn "  %-34s %-16s %s" e.Name e.Widget e.Note

    let private parseTheme (themeStr: string) : PaletteMode list =
        match themeStr.ToLowerInvariant() with
        | "light" -> [ PaletteMode.Light ]
        | "dark" -> [ PaletteMode.Dark ]
        | "both" -> [ PaletteMode.Light; PaletteMode.Dark ]
        | other ->
            eprintfn "未知主题模式：%s，支持 light | dark | both（默认 both）" other
            [ PaletteMode.Light; PaletteMode.Dark ]

    [<EntryPoint>]
    let main argv =
        let args = Array.toList argv

        match args with
        | [ "--list" ] ->
            printfn "%s" (Gallery.listText ())
            0
        | [ "--window" ] ->
            // 交互式画廊窗口：这是画廊的**开发用法**——
            // 改一个控件库 → 只重建那个库 → 重开窗口就看到新样子，不需要连主机、不需要打整个 feed。
            // 走真实桌面平台（Avalonia.Desktop），窗口由系统画出来，可以点。
            GalleryDesktopApp.OnReady <-
                Some(fun () ->
                    let ctx = Gallery.createContext ()
                    GalleryDesktopApp.Window <- Some(Wanxiang2.Gallery.Window.build ctx None))

            AppBuilder
                .Configure<GalleryDesktopApp>()
                .UsePlatformDetect()
                .StartWithClassicDesktopLifetime([||], ShutdownMode.OnMainWindowClose)
            |> ignore

            0
        | [ "--render"; name; outPath ]
        | [ "--render"; name; outPath; "--theme"; _ ] ->
            let mode =
                if args.Length >= 5 then
                    match args.[4].ToLowerInvariant() with
                    | "dark" -> PaletteMode.Dark
                    | _ -> PaletteMode.Light
                else
                    PaletteMode.Light

            match Gallery.find name with
            | None ->
                eprintfn "未知条目：%s" name
                eprintfn "用 --list 看全部条目。"
                2
            | Some entry ->
                let session =
                    HeadlessUnitTestSession.StartNew(typeof<GalleryAppBuilder>, AvaloniaTestIsolationLevel.PerTest)

                let mutable failure: string option = None
                // 必须等 headless 会话把这段跑完（Dispatch 返回 Task）；不等的话进程会先退出。
                session
                    .Dispatch(
                        (Action(fun () ->
                            try
                                Gallery.setMode mode
                                let ctx = Gallery.createContextMode mode
                                let control = Gallery.build ctx entry

                                let win =
                                    Window(
                                        Width = control.Bounds.Width,
                                        Height = control.Bounds.Height,
                                        Content = control
                                    )

                                win.Show()
                                win.UpdateLayout()
                                control.InvalidateVisual()
                                win.UpdateLayout()
                                let bytes = Render.toPng control outPath
                                printfn "%s (%s) → %s（%d 字节）" entry.Name (Baseline.themeKey mode) outPath bytes
                            with ex ->
                                failure <- Some ex.Message)),
                        CancellationToken.None
                    )
                    .GetAwaiter()
                    .GetResult()

                match failure with
                | Some msg ->
                    eprintfn "渲染失败：%s" msg
                    1
                | None -> 0
        | [ "--check-baselines"; dir ]
        | [ "--check-baselines"; dir; "--theme"; _ ] ->
            let modes =
                if args.Length >= 4 then
                    parseTheme args.[3]
                else
                    [ PaletteMode.Light; PaletteMode.Dark ]

            let session =
                HeadlessUnitTestSession.StartNew(typeof<GalleryAppBuilder>, AvaloniaTestIsolationLevel.PerTest)

            let mutable exitCode = 0

            session
                .Dispatch(
                    (Action(fun () ->
                        printfn "== 开始画廊深浅主题 PNG 像素基线核查（基线目录：%s）==" dir
                        let results, allPassed = Baseline.checkAll dir Gallery.entries modes

                        for res in results do
                            match res with
                            | BaselineCheck.Passed(entry, theme) -> printfn "  [PASS] %-34s (%s)" entry theme
                            | BaselineCheck.MissingBaseline(entry, theme, path) ->
                                eprintfn "  [FAIL MISSING] %-34s (%s) 缺少基线：%s" entry theme path
                            | BaselineCheck.SizeMismatch(entry, theme, (ew, eh), (aw, ah)) ->
                                eprintfn "  [FAIL SIZE] %-34s (%s) 期望 %dx%d，实际 %dx%d" entry theme ew eh aw ah
                            | BaselineCheck.PixelChanged(entry, theme, diffCount, total, (x, y, _, _), diffPath) ->
                                let pct = (float diffCount / float total) * 100.0

                                eprintfn
                                    "  [FAIL PIXEL_DIFF] %-34s (%s) %d/%d 像素不同 (%.2f%%)，首个差异点 (%d,%d)；diff 图：%s"
                                    entry
                                    theme
                                    diffCount
                                    total
                                    pct
                                    x
                                    y
                                    (defaultArg diffPath "无")
                            | BaselineCheck.RenderError(entry, theme, msg) ->
                                eprintfn "  [FAIL ERROR] %-34s (%s) 异常：%s" entry theme msg

                        if allPassed then
                            printfn "\n基线核查全部通过（共 %d 项测试）。" results.Length
                            exitCode <- 0
                        else
                            eprintfn "\n基线核查失败！存在像素差异或缺少基线文件（基线必须由 DevOps 生成，缺基线必须红）。"
                            exitCode <- 1)),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult()

            exitCode
        | [ "--generate-baselines"; dir ]
        | [ "--generate-baselines"; dir; "--theme"; _ ] ->
            let modes =
                if args.Length >= 4 then
                    parseTheme args.[3]
                else
                    [ PaletteMode.Light; PaletteMode.Dark ]

            let session =
                HeadlessUnitTestSession.StartNew(typeof<GalleryAppBuilder>, AvaloniaTestIsolationLevel.PerTest)

            session
                .Dispatch(
                    (Action(fun () ->
                        printfn "== 开始生成画廊深浅主题 PNG 像素基线（输出目录：%s）==" dir
                        let list = Baseline.generateAll dir Gallery.entries modes

                        for (entry, theme, path, bytes) in list do
                            printfn "  [GEN] %-34s (%s) → %s (%d 字节)" entry theme path bytes

                        printfn "\n已成功生成 %d 张基线图片。" list.Length)),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult()

            0
        | [ "--a11y-audit" ] ->
            let session =
                HeadlessUnitTestSession.StartNew(typeof<GalleryAppBuilder>, AvaloniaTestIsolationLevel.PerTest)

            let mutable allDefects = []
            let mutable totalAudited = 0

            session
                .Dispatch(
                    (Action(fun () ->
                        printfn "== 开始画廊真实控件树无障碍遍历审查（G5）=="

                        for entry in Gallery.entries do
                            let ctx = Gallery.createContextMode PaletteMode.Light
                            let control = Gallery.build ctx entry

                            let win =
                                Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

                            win.Show()
                            win.UpdateLayout()
                            control.InvalidateVisual()
                            win.UpdateLayout()
                            let items = A11yAudit.auditWindow win
                            totalAudited <- totalAudited + items.Length
                            let defects = A11yAudit.defectsOf items
                            let isStaticExempt = entry.Widget = "ChatEmpty" && entry.Name = "ChatEmpty.skeleton"

                            if not defects.IsEmpty then
                                eprintfn "  [DEFECT] 条目 %s (发现 %d 处缺陷):" entry.Name defects.Length
                                eprintfn "%s" (A11yAudit.formatDefects defects)
                                allDefects <- allDefects @ defects
                            elif items.IsEmpty && not isStaticExempt then
                                eprintfn "  [DEFECT] 交互性条目 %s 审查到 0 个受检控件（禁止恒空通过）" entry.Name

                                allDefects <-
                                    allDefects
                                    @ [
                                        A11yDefect.MissingAccessibleName(
                                            entry.Name,
                                            AutomationControlType.Custom,
                                            "交互性条目未扫描到任何受检可交互控件"
                                        )
                                    ]
                            elif isStaticExempt then
                                printfn "  [PASS (EXEMPT)] %-34s 纯展示性条目（不适用交互控件审查）" entry.Name
                            else
                                printfn "  [PASS] %-34s 控件树 %d 个受检控件审查通过" entry.Name items.Length

                            win.Close()

                        if allDefects.IsEmpty then
                            printfn
                                "\n无障碍审查全部通过：已遍历全画廊 %d 个条目，审查了 %d 个受检可交互/可聚焦控件，0 缺陷。"
                                Gallery.entries.Length
                                totalAudited
                        else
                            eprintfn "\n无障碍审查失败：全画廊共发现 %d 处缺陷！" allDefects.Length)),
                    CancellationToken.None
                )
                .GetAwaiter()
                .GetResult()

            if allDefects.IsEmpty then 0 else 1
        | _ ->
            usage ()
            2
