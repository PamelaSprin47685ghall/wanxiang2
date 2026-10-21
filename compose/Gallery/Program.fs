module Wanxiang2.Gallery.Program

open System
open System.IO
open System.Threading
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.ApplicationLifetimes
open Avalonia.Headless
open Avalonia.Media
open Avalonia.Media.Imaging
open Avalonia.Styling
open Avalonia.Themes.Fluent

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
        eprintfn "  dotnet run --project compose/Gallery/Gallery.fsproj -- --render <条目名> <输出.png>"
        eprintfn ""
        eprintfn "条目："

        for e in Gallery.entries do
            eprintfn "  %-34s %-16s %s" e.Name e.Widget e.Note

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
        | [ "--render"; name; outPath ] ->
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
                                let ctx = Gallery.createContext ()
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
                                printfn "%s → %s（%d 字节）" entry.Name outPath bytes
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
        | _ ->
            usage ()
            2
