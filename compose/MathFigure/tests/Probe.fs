module Probe

open System
open System.Security.Cryptography
open System.Text
open Avalonia
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Media
open Avalonia.Media.Imaging

/// 金样两边共用：同一段落、同一画布，量出布局（Bounds、基线、留白、读屏名）。
/// 不记像素哈希：Skia 在 macOS（CoreText）与 Linux（FreeType）上光栅化结果不同，布局与基线则平台无关。
let f (v: float) =
    v.ToString("0.###", Globalization.CultureInfo.InvariantCulture)

let ink = SolidColorBrush(Color.Parse "#1F2328")

/// 整块像素的 SHA-256 前缀。只用于同一进程内的前后对比，不进金样（跨平台不稳定）。
let pixels (control: Control) =
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

    Convert.ToHexString(SHA256.HashData buffer).Substring(0, 16).ToLowerInvariant()

let private show (content: Control) =
    let win = Window(Width = 480.0, Height = 400.0, Content = content)
    win.Show()
    win.UpdateLayout()
    win

/// 一条公式的探测结果：行内放进段落（前后有正文），展示式放进竖排容器。
/// tryInline/tryBlock/container/baseline 由各自实现给出；body 是正文字体。
let probe
    (body: FontFamily)
    (tryInline: unit -> Control option)
    (tryBlock: unit -> Control option)
    (container: Control -> InlineUIContainer)
    (baseline: Control -> float)
    (displayMode: bool)
    : string =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore

    if displayMode then
        match tryBlock () with
        | None -> line "block: none"
        | Some host ->
            let panel = StackPanel(Width = 400.0)
            panel.Children.Add(TextBlock(Text = "前文", FontFamily = body, FontSize = 14.5, Foreground = ink))
            panel.Children.Add host
            let win = show panel
            let figure = (host :?> Border).Child

            line (
                sprintf
                    "block: host=%s,%s,%s,%s figure=%s,%s,%s,%s baseline=%s pad=%s"
                    (f host.Bounds.X)
                    (f host.Bounds.Y)
                    (f host.Bounds.Width)
                    (f host.Bounds.Height)
                    (f figure.Bounds.X)
                    (f figure.Bounds.Y)
                    (f figure.Bounds.Width)
                    (f figure.Bounds.Height)
                    (f (baseline figure))
                    (f (host :?> Border).Padding.Top)
            )

            win.Close()
    else
        match tryInline () with
        | None -> line "inline: none"
        | Some figure ->
            let block =
                TextBlock(
                    FontFamily = body,
                    FontSize = 14.5,
                    LineHeight = 14.5 * 1.62,
                    Foreground = ink,
                    Width = 400.0,
                    TextWrapping = TextWrapping.Wrap
                )

            block.Inlines <- InlineCollection()
            block.Inlines.Add(Run "前文 x ")
            block.Inlines.Add(container figure)
            block.Inlines.Add(Run " 后文")
            let win = show block
            block.InvalidateVisual()
            win.UpdateLayout()

            line (
                sprintf
                    "inline: block=%s,%s figure=%s,%s,%s,%s baseline=%s name=%s"
                    (f block.Bounds.Width)
                    (f block.Bounds.Height)
                    (f figure.Bounds.X)
                    (f figure.Bounds.Y)
                    (f figure.Bounds.Width)
                    (f figure.Bounds.Height)
                    (f (baseline figure))
                    (string (Avalonia.Automation.AutomationProperties.GetName figure))
            )

            win.Close()

    sb.ToString()
