namespace Wanxiang2.Gallery.Tests

open System
open System.IO
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Media.Imaging
open Xunit
open Wanxiang2.Gallery
open Wanxiang2.ThemePalette

module BaselineTests =

    [<Fact>]
    let ``缺少基线文件时核查必须红（MissingBaseline）`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "ComposerBox.idle" |> Option.get

            let nonExistentDir =
                Path.Combine(Path.GetTempPath(), "wx-test-baselines-" + Guid.NewGuid().ToString("N"))

            let result = Baseline.checkEntry nonExistentDir entry PaletteMode.Light

            match result with
            | BaselineCheck.MissingBaseline(e, theme, path) ->
                Assert.Equal(entry.Name, e)
                Assert.Equal("light", theme)
                Assert.Contains("ComposerBox.idle.light.png", path)
            | other -> failwithf "期望缺失基线报告红灯，实际返回: %A" other)

    [<Fact>]
    let ``PNG 往返保持红蓝通道与像素值`` () =
        Headless.run (fun () ->
            use bitmap =
                new WriteableBitmap(
                    PixelSize(2, 1),
                    Vector(96.0, 96.0),
                    Avalonia.Platform.PixelFormat.Bgra8888,
                    Avalonia.Platform.AlphaFormat.Premul
                )

            let expected = [| 0xFF123456u; 0xFFABCDEFu |]
            let bytes = Array.zeroCreate<byte> 8
            Buffer.BlockCopy(expected, 0, bytes, 0, bytes.Length)

            (use frame = bitmap.Lock()
             Runtime.InteropServices.Marshal.Copy(bytes, 0, frame.Address, bytes.Length))

            use stream = new MemoryStream()
            bitmap.Save(stream, PngBitmapEncoderOptions())
            stream.Position <- 0L
            use loaded = new Bitmap(stream)
            let _, _, actual = Baseline.extractPixels loaded
            Assert.Equal<uint32>(expected, actual))

    [<Fact>]
    let ``同图比较必定 100% 匹配（Match）`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "ComposerBox.idle" |> Option.get
            let ctx = Gallery.createContextMode PaletteMode.Light
            let bmp1, win1 = Baseline.renderEntry ctx entry PaletteMode.Light

            try
                let diff, diffBmp = Baseline.compareBitmaps bmp1 bmp1
                Assert.Equal(DiffResult.Match, diff)
                Assert.True(diffBmp.IsNone)
            finally
                win1.Close()
                bmp1.Dispose())

    [<Fact>]
    let ``像素改变时核查必须红并指出坐标（PixelMismatch）`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "ComposerBox.idle" |> Option.get

            let tempDir =
                Path.Combine(Path.GetTempPath(), "wx-diff-test-" + Guid.NewGuid().ToString("N"))

            Directory.CreateDirectory tempDir |> ignore

            try
                // 1. 生成一份基准位图并保存
                let ctx = Gallery.createContextMode PaletteMode.Light
                let bmpOriginal, winOrig = Baseline.renderEntry ctx entry PaletteMode.Light
                let path = Baseline.baselinePath tempDir entry.Name PaletteMode.Light
                Baseline.saveBitmap bmpOriginal path |> ignore
                winOrig.Close()
                bmpOriginal.Dispose()

                // 2. 模拟盘上基线被篡改了 1 个像素
                let w = int Gallery.canvasWidth
                let h = int Gallery.canvasHeight
                use modBmp = new Bitmap(path)
                let _, _, pixels = Baseline.extractPixels modBmp
                // 翻转第 (100, 100) 处的像素
                let targetIdx = 100 * w + 100
                pixels.[targetIdx] <- pixels.[targetIdx] ^^^ 0x00FFFFFFu
                let hackedPath = path
                let buffer = Array.zeroCreate<byte>(w * 4 * h)
                Buffer.BlockCopy(pixels, 0, buffer, 0, buffer.Length)

                let hackedBmp =
                    new WriteableBitmap(
                        PixelSize(w, h),
                        Vector(96.0, 96.0),
                        Avalonia.Platform.PixelFormat.Bgra8888,
                        Avalonia.Platform.AlphaFormat.Premul
                    )

                (use f = hackedBmp.Lock()
                 Runtime.InteropServices.Marshal.Copy(buffer, 0, f.Address, buffer.Length))

                hackedBmp.Save(hackedPath, PngBitmapEncoderOptions())
                hackedBmp.Dispose()

                // 3. 执行核查：必定检出 PixelChanged 且指出坐标与差异
                let check = Baseline.checkEntry tempDir entry PaletteMode.Light

                match check with
                | BaselineCheck.PixelChanged(e, theme, diffCount, total, (x, y, _, _), diffPathOpt) ->
                    Assert.Equal(entry.Name, e)
                    Assert.Equal("light", theme)
                    Assert.True(diffCount >= 1)
                    Assert.Equal(w * h, total)
                    Assert.Equal(100, x)
                    Assert.Equal(100, y)
                    Assert.True(diffPathOpt.IsSome)
                    Assert.True(File.Exists diffPathOpt.Value)
                | other -> failwithf "期望检出像素改变，实际返回: %A" other
            finally
                if Directory.Exists tempDir then
                    Directory.Delete(tempDir, true))

    [<Fact>]
    let ``尺寸不匹配时核查必须红（SizeMismatch）`` () =
        Headless.run (fun () ->
            use bmp1 = new RenderTargetBitmap(PixelSize(100, 100), Vector(96.0, 96.0))
            use bmp2 = new RenderTargetBitmap(PixelSize(200, 200), Vector(96.0, 96.0))
            let diff, _ = Baseline.compareBitmaps bmp1 bmp2

            match diff with
            | DiffResult.SizeMismatch((w1, h1), (w2, h2)) ->
                Assert.Equal(100, w1)
                Assert.Equal(100, h1)
                Assert.Equal(200, w2)
                Assert.Equal(200, h2)
            | other -> failwithf "期望尺寸不一致，实际返回: %A" other)

    [<Fact>]
    let ``深浅两套主题渲染出的位图背景与像素真实不同`` () =
        Headless.run (fun () ->
            let entry = Gallery.find "ComposerBox.idle" |> Option.get
            let ctxLight = Gallery.createContextMode PaletteMode.Light
            let bmpLight, winLight = Baseline.renderEntry ctxLight entry PaletteMode.Light

            let ctxDark = Gallery.createContextMode PaletteMode.Dark
            let bmpDark, winDark = Baseline.renderEntry ctxDark entry PaletteMode.Dark

            try
                let diff, _ = Baseline.compareBitmaps bmpLight bmpDark

                match diff with
                | DiffResult.PixelMismatch(diffCount, _, (_, _, cLight, cDark)) ->
                    // 深色模式与浅色模式在画布与控件各处必有大量不同像素
                    Assert.True(diffCount > 1000)
                    Assert.NotEqual<Color>(cLight, cDark)
                | other -> failwithf "深浅两套主题渲染结果必须不同，实际为: %A" other
            finally
                winLight.Close()
                bmpLight.Dispose()
                winDark.Close()
                bmpDark.Dispose())
