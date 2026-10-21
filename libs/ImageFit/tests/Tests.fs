module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

[<Fact>]
let ``332 放不下时按 0.75 逐轮缩边，最长边不超过 4096`` () =
    let limits =
        List.scan
            (fun l _ -> ImageFit.nextLimit (ImageFit.fit (12000, 3000) l))
            ImageFit.maxDimension
            [ 1 .. ImageFit.maxAttempts - 1 ]

    Assert.Equal<int list>([ 4096; 3072; 2304; 1728; 1296; 972; 729; 546 ], limits)
    Assert.Equal((4096, 1024), ImageFit.fit (12000, 3000) 4096)
    Assert.True(ImageFit.tooLarge (4097, 1))
    Assert.False(ImageFit.tooLarge (4096, 4096))

[<Fact>]
let ``332 有透明通道的 PNG/WebP 先试 PNG，其余只试 JPEG 85/75/65`` () =
    Assert.Equal<int list>(
        [ 100; 85; 75; 65 ],
        ImageFit.candidates (ImageFit.keepsAlpha false "image/webp")
        |> List.map (fun (e, _, _) -> ImageFit.quality e)
    )

    Assert.Equal<string list>(
        [ ".jpg"; ".jpg"; ".jpg" ],
        ImageFit.candidates (ImageFit.keepsAlpha false "image/jpeg")
        |> List.map (fun (_, x, _) -> x)
    )

    Assert.False(ImageFit.keepsAlpha true "image/png")

[<Fact>]
let ``342 平台缩小的结果按媒体类型改名`` () =
    Assert.Equal("wide.png", ImageFit.renamedFor "wide.webp" "image/png")
    Assert.Equal("image.jpg", ImageFit.renamedFor ".png" "image/jpeg")
