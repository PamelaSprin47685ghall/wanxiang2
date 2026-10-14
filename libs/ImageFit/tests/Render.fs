/// 金标准渲染：同一份源码分别对旧程序集与新库编译。
/// 旧侧真的造一张纯色图交给旧 ImageShrink.shrink；纯色图第一种编码就能落在 8 MiB 内，所以结果尺寸就是 fit 4096、格式就是候选第一项。
/// 新侧用本库的规则算出同样的结果；文件名错误文案两侧都直接调用。
module Render

open System
open Wanxiang.UI

let cases =
    [ "photo.png", 5000, 3000, false
      "clear.png", 3000, 5000, true
      "wide.webp", 8000, 10, true
      "flat.webp", 4097, 4097, false
      "shot.jpg", 6000, 4000, false
      ".png", 4500, 100, false
      "a.b.png", 1, 9000, true ]

#if INTERACTIVE
open SkiaSharp

let private run (name: string, w: int, h: int, alpha: bool) =
    use bmp = new SKBitmap(SKImageInfo(w, h, SKColorType.Rgba8888, (if alpha then SKAlphaType.Unpremul else SKAlphaType.Opaque)))
    bmp.Erase(if alpha then SKColor(10uy, 20uy, 30uy, 128uy) else SKColor(10uy, 20uy, 30uy))
    use img = SKImage.FromBitmap bmp
    let fmt =
        if name.EndsWith ".webp" then SKEncodedImageFormat.Webp
        elif name.EndsWith ".jpg" then SKEncodedImageFormat.Jpeg
        else SKEncodedImageFormat.Png
    use data = img.Encode(fmt, 100)
    match ImageShrink.shrink name (data.ToArray()) with
    | Ok(Some r) -> sprintf "%s %dx%d -> %s %s %dx%d" name w h r.fileName r.mediaType r.width r.height
    | other -> sprintf "%s %A" name other

let private texts name =
    // 旧文案只出现在 shrink 的分支里；这几行直接写出旧源码中的同一格式串，由新侧逐字比对。
    [ sprintf "没有添加「%s」：动图超过 8 MiB，缩小会丢掉动画。请先压缩动图，或换成静态图片。" name
      sprintf "没有添加「%s」：图片无法读取，可能已损坏。请换一张图片。" name
      sprintf "没有添加「%s」：图片无法读取（%s）。请换一张图片。" name "boom"
      sprintf "没有添加「%s」：自动缩小后仍超过 8 MiB。请截取一部分后再添加。" name ]
#else
let private mediaOf (name: string) =
    if name.EndsWith ".webp" then "image/webp" elif name.EndsWith ".jpg" then "image/jpeg" else "image/png"

let private run (name: string, w: int, h: int, alpha: bool) =
    let opaque = not alpha || name.EndsWith ".jpg"
    let _, ext, mt = ImageFit.candidates (ImageFit.keepsAlpha opaque (mediaOf name)) |> List.head
    let fw, fh = ImageFit.fit (w, h) ImageFit.maxDimension
    sprintf "%s %dx%d -> %s %s %dx%d" name w h (ImageFit.renamed name ext) mt fw fh

let private texts name =
    [ ImageFit.animatedTooLarge name; ImageFit.unreadable name; ImageFit.unreadableBecause name "boom"; ImageFit.stillTooLarge name ]
#endif

let all () =
    [ for c in cases -> run c
      yield! texts "图.png" ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
