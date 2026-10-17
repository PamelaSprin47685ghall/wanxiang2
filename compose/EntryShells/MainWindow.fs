namespace Wanxiang.UI

open System
open Avalonia
open Avalonia.Controls
open Wanxiang2.ThemePalette

/// 桌面窗口壳（迁自宿主 src/Wanxiang.UI/Views/MainWindow.fs）。
///
/// UI 主体在 `MainView`（决策 48：桌面与 PWA 共用同一套视图；MainView 由 AppShell 组合者提供，
/// 经注入端口接入）；这里只负责窗口本身：尺寸、位置与它们的本机持久化（Q195）。
/// 几何持久化是文件 IO，由注入端口提供（LoadPrefs / SavePrefs），本壳不碰文件。
type MainWindowPort =
    { /// 创建根视图（MainView 工厂，由入口注入）
      Root: unit -> Control
      /// 视图构建（AppShell.Build，由入口注入）
      Build: Control -> unit
      /// 关窗落草稿（AppShell.FlushDrafts，由入口注入）
      FlushDrafts: unit -> unit
      /// 读几何偏好（文件 IO，由入口注入）
      LoadPrefs: unit -> UiPrefs
      /// 写几何偏好（文件 IO，由入口注入）
      SavePrefs: UiPrefs -> unit
      /// 采样模式首帧打点（可选；进程退出等副作用由入口注入）
      MarkFirstFrame: (unit -> unit) option }

type MainWindow(port: MainWindowPort) as this =
    inherit Window()

    let view = port.Root ()
    let mutable prefs = port.LoadPrefs ()

    do
        this.Title <- "万象"
        this.MinWidth <- LayoutPolicy.desktopMinWidth
        this.MinHeight <- 620.0

        // D7：Closing 存 Width/Height/X/Y，启动同样恢复 Width；下限与 MinWidth 同源，
        // 上限钳到屏幕最大可用宽，避免换显示器后窗口超出可视区。
        let maxWidth =
            try
                let widths = this.Screens.All |> Seq.map (fun s -> float s.WorkingArea.Width) |> Seq.toList
                match widths with [] -> prefs.windowWidth | _ -> List.max widths
            with _ ->
                prefs.windowWidth
        this.Width <- Math.Clamp(prefs.windowWidth, LayoutPolicy.desktopMinWidth, Math.Max(LayoutPolicy.desktopMinWidth, maxWidth))
        this.Height <- prefs.windowHeight
        this.WindowStartupLocation <-
            if prefs.hasWindowPosition then WindowStartupLocation.Manual else WindowStartupLocation.CenterScreen
        if prefs.hasWindowPosition then
            this.Position <- PixelPoint(prefs.windowX, prefs.windowY)
        this.Background <- SharedTheme.manager.Brush PaletteKey.Canvas
        this.Content <- view
        port.Build view

        SharedTheme.manager.Changed.Add(fun _ -> this.Background <- SharedTheme.manager.Brush PaletteKey.Canvas)

        // TODO(入口壳)：宿主 MainWindow 的 StartupTrace 采样退出（requestExit = Environment.Exit 0）
        // 是进程级副作用，由入口注入的 MarkFirstFrame 承担；采样退出本身留给 app/Desktop 接线。
        port.MarkFirstFrame |> Option.iter (fun mark ->
            this.Opened.Add(fun _ -> this.RequestAnimationFrame(fun _ -> mark ())))

        this.Closing.Add(fun _ ->
            // 先落草稿：关窗口前最后一次输入也要在本机留下（W5，决策 80）。
            port.FlushDrafts ()
            // 以磁盘上的最新偏好为底再写窗口几何：设置页改过的开关不能被构造时读到的旧副本覆盖回去。
            prefs <-
                { port.LoadPrefs () with
                    windowWidth = this.Width
                    windowHeight = this.Height
                    windowX = this.Position.X
                    windowY = this.Position.Y
                    hasWindowPosition = true }
            port.SavePrefs prefs)
