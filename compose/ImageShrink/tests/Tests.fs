module Tests

// 迁自旧 R16ImageVisionTests 中不需要 Skia 的断言：判定与说明文案。真实编解码的部分留给宿主。

open Xunit
open Wanxiang.UI

[<Fact>]
let ``normal images and non-images are left alone`` () =
    Assert.False(ImageShrink.needsShrinkFor "a.png" 2_000L (Some(800, 600)))
    Assert.False(ImageShrink.needsShrinkFor "big.pdf" (9L * 1024L * 1024L) None)
    // 读不出来的“图片”不抛异常。
    Assert.False(ImageShrink.needsShrinkFor "broken.png" 3L None)

[<Fact>]
let ``oversize or huge images need shrinking`` () =
    Assert.True(ImageShrink.needsShrinkFor "photo.png" (ImageShrink.maxBytes + 1L) (Some(3000, 2400)))
    Assert.True(ImageShrink.needsShrinkFor "wide.png" 1_000L (Some(9000, 1200)))
    Assert.Equal(4096, ImageShrink.maxDimension)

[<Fact>]
let ``oversize photo note names before and after`` () =
    let r =
        {
            bytes = Array.zeroCreate 1536
            fileName = "photo.jpg"
            mediaType = "image/jpeg"
            originalBytes = 9L * 1024L * 1024L
            originalWidth = 3000
            originalHeight = 2400
            width = 3000
            height = 2400
        }

    let note = ImageShrink.note r
    Assert.Contains("原图", note)
    Assert.Contains("→ 发送", note)
    Assert.Contains("3000×2400", note)
    Assert.Equal("已自动缩小：原图 9.0 MiB（3000×2400）→ 发送 1.5 KiB（3000×2400）", note)
