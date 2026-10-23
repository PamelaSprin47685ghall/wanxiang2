namespace Wanxiang2.Gallery

open System
open System.IO
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Media.Imaging
open Wanxiang2.ThemePalette

/// 像素差异判定结果
[<RequireQualifiedAccess>]
type DiffResult =
    /// 逐像素完全一致（100% 匹配）
    | Match
    /// 尺寸不一致：期望 (w, h)，实际 (w, h)
    | SizeMismatch of expected: (int * int) * actual: (int * int)
    /// 像素有差异：diffCount 是不同像素数，totalPixels 是总像素数，firstDiff 是首个差异点 (x, y, 期望色, 实际色)
    | PixelMismatch of diffCount: int * totalPixels: int * firstDiff: (int * int * Color * Color)

/// 单个条目的基线核查状态（缺基线或像素改变均必须红）
[<RequireQualifiedAccess>]
type BaselineCheck =
    /// 通过：像素与基线完全一致
    | Passed of entry: string * theme: string
    /// 缺少基线文件（必须红，基线由 DevOps 后续实际生成，不能伪造，不能静默放行）
    | MissingBaseline of entry: string * theme: string * path: string
    /// 尺寸不匹配（必须红）
    | SizeMismatch of entry: string * theme: string * expected: (int * int) * actual: (int * int)
    /// 像素变化（必须红）
    | PixelChanged of
        entry: string *
        theme: string *
        diffCount: int *
        totalPixels: int *
        firstDiff: (int * int * Color * Color) *
        diffPath: string option
    /// 渲染或加载异常
    | RenderError of entry: string * theme: string * message: string

module Baseline =

    /// 从位图提取像素数据为 uint32 数组（以 4 字节每像素处理）
    let extractPixels (bmp: Bitmap) : int * int * uint32[] =
        let w = bmp.PixelSize.Width
        let h = bmp.PixelSize.Height
        let stride = w * 4
        let buffer = Array.zeroCreate<byte>(stride * h)
        let handle = GCHandle.Alloc(buffer, GCHandleType.Pinned)

        try
            bmp.CopyPixels(PixelRect(0, 0, w, h), handle.AddrOfPinnedObject(), buffer.Length, stride)
        finally
            handle.Free()

        let pixels = Array.zeroCreate<uint32>(w * h)
        Buffer.BlockCopy(buffer, 0, pixels, 0, buffer.Length)
        w, h, pixels

    /// 对比两张位图：返回差异结果与可选的高亮差异位图
    let compareBitmaps (baselineBmp: Bitmap) (actualBmp: Bitmap) : DiffResult * Bitmap option =
        let bw, bh = baselineBmp.PixelSize.Width, baselineBmp.PixelSize.Height
        let aw, ah = actualBmp.PixelSize.Width, actualBmp.PixelSize.Height

        if bw <> aw || bh <> ah then
            DiffResult.SizeMismatch((bw, bh), (aw, ah)), None
        else
            let _, _, bPixels = extractPixels baselineBmp
            let _, _, aPixels = extractPixels actualBmp
            let total = bw * bh
            let mutable diffCount = 0
            let mutable firstDiff = None

            for i in 0 .. total - 1 do
                let bp = bPixels.[i]
                let ap = aPixels.[i]

                if bp <> ap then
                    diffCount <- diffCount + 1

                    if firstDiff.IsNone then
                        let x = i % bw
                        let y = i / bw

                        let bc =
                            Color.FromArgb(
                                byte (bp >>> 24),
                                byte ((bp >>> 16) &&& 0xFFu),
                                byte ((bp >>> 8) &&& 0xFFu),
                                byte (bp &&& 0xFFu)
                            )

                        let ac =
                            Color.FromArgb(
                                byte (ap >>> 24),
                                byte ((ap >>> 16) &&& 0xFFu),
                                byte ((ap >>> 8) &&& 0xFFu),
                                byte (ap &&& 0xFFu)
                            )

                        firstDiff <- Some(x, y, bc, ac)

            if diffCount = 0 then
                DiffResult.Match, None
            else
                // 构造高亮差异位图：相同像素降低对比度变暗淡，差异像素标为高亮品红 (#FFFF00FF)
                let diffBuffer = Array.zeroCreate<byte>(bw * 4 * bh)

                for i in 0 .. total - 1 do
                    let offset = i * 4

                    if bPixels.[i] = aPixels.[i] then
                        let c = aPixels.[i]
                        let b = byte (c &&& 0xFFu)
                        let g = byte ((c >>> 8) &&& 0xFFu)
                        let r = byte ((c >>> 16) &&& 0xFFu)
                        let gray = byte (float r * 0.299 + float g * 0.587 + float b * 0.114)
                        let muted = byte (float gray * 0.35)
                        diffBuffer.[offset] <- muted
                        diffBuffer.[offset + 1] <- muted
                        diffBuffer.[offset + 2] <- muted
                        diffBuffer.[offset + 3] <- 255uy
                    else
                        // 品红高亮差异 (BGRA: 255, 0, 255, 255)
                        diffBuffer.[offset] <- 255uy
                        diffBuffer.[offset + 1] <- 0uy
                        diffBuffer.[offset + 2] <- 255uy
                        diffBuffer.[offset + 3] <- 255uy

                let diffBmp =
                    new WriteableBitmap(
                        PixelSize(bw, bh),
                        Vector(96.0, 96.0),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Premul
                    )

                use frame = diffBmp.Lock()
                Marshal.Copy(diffBuffer, 0, frame.Address, diffBuffer.Length)
                DiffResult.PixelMismatch(diffCount, total, firstDiff.Value), Some(diffBmp :> Bitmap)

    /// 渲染一个条目成内存位图与承载窗口
    let renderEntry (ctx: GalleryContext) (entry: Entry) (mode: PaletteMode) : RenderTargetBitmap * Window =
        Gallery.setMode mode
        let control = Gallery.build ctx entry

        let win =
            Window(Width = Gallery.canvasWidth, Height = Gallery.canvasHeight, Content = control)

        win.Show()
        win.UpdateLayout()
        control.InvalidateVisual()
        win.UpdateLayout()
        let w = max 1 (int (Math.Ceiling control.Bounds.Width))
        let h = max 1 (int (Math.Ceiling control.Bounds.Height))
        let bmp = new RenderTargetBitmap(PixelSize(w, h), Vector(96.0, 96.0))
        bmp.Render control
        bmp, win

    /// 保存位图到 PNG 文件并返回字节数
    let saveBitmap (bmp: Bitmap) (path: string) : int =
        let dir = Path.GetDirectoryName path

        if not (String.IsNullOrEmpty dir) && not (Directory.Exists dir) then
            Directory.CreateDirectory dir |> ignore

        bmp.Save(path, PngBitmapEncoderOptions())
        FileInfo(path).Length |> int

    /// 主题模式字符串
    let themeKey (mode: PaletteMode) : string =
        match mode with
        | PaletteMode.Light -> "light"
        | PaletteMode.Dark -> "dark"

    /// 基线文件名
    let baselinePath (baselineDir: string) (entryName: string) (mode: PaletteMode) : string =
        Path.Combine(baselineDir, sprintf "%s.%s.png" entryName (themeKey mode))

    /// 核对单个条目在指定主题下的基线
    let checkEntry (baselineDir: string) (entry: Entry) (mode: PaletteMode) : BaselineCheck =
        let path = baselinePath baselineDir entry.Name mode
        let themeStr = themeKey mode

        if not (File.Exists path) then
            BaselineCheck.MissingBaseline(entry.Name, themeStr, path)
        else
            try
                use baselineBmp = new Bitmap(path)
                let ctx = Gallery.createContextMode mode
                let actualBmp, win = renderEntry ctx entry mode

                try
                    let diff, diffBmpOpt = compareBitmaps baselineBmp actualBmp

                    match diff with
                    | DiffResult.Match -> BaselineCheck.Passed(entry.Name, themeStr)
                    | DiffResult.SizeMismatch(exp, act) -> BaselineCheck.SizeMismatch(entry.Name, themeStr, exp, act)
                    | DiffResult.PixelMismatch(diffCount, total, first) ->
                        let diffPath =
                            match diffBmpOpt with
                            | Some diffBmp ->
                                let dp =
                                    Path.Combine(baselineDir, "diff", sprintf "%s.%s.diff.png" entry.Name themeStr)

                                saveBitmap diffBmp dp |> ignore
                                Some dp
                            | None -> None

                        BaselineCheck.PixelChanged(entry.Name, themeStr, diffCount, total, first, diffPath)
                finally
                    win.Close()
                    actualBmp.Dispose()
            with ex ->
                BaselineCheck.RenderError(entry.Name, themeStr, ex.Message)

    /// 对所有条目和主题进行基线核查
    let checkAll (baselineDir: string) (entries: Entry list) (modes: PaletteMode list) : BaselineCheck list * bool =
        let results =
            [
                for entry in entries do
                    for mode in modes do
                        checkEntry baselineDir entry mode
            ]

        let allPassed =
            results
            |> List.forall (function
                | BaselineCheck.Passed _ -> true
                | _ -> false)

        results, allPassed

    /// 生成全量基线图片（DevOps 用）
    let generateAll
        (outputDir: string)
        (entries: Entry list)
        (modes: PaletteMode list)
        : (string * string * string * int) list =
        if not (Directory.Exists outputDir) then
            Directory.CreateDirectory outputDir |> ignore

        [
            for entry in entries do
                for mode in modes do
                    let themeStr = themeKey mode
                    let path = baselinePath outputDir entry.Name mode
                    let ctx = Gallery.createContextMode mode
                    let bmp, win = renderEntry ctx entry mode

                    try
                        let bytes = saveBitmap bmp path
                        entry.Name, themeStr, path, bytes
                    finally
                        win.Close()
                        bmp.Dispose()
        ]
