namespace Wanxiang.UI

open System
open System.IO

/// 一次编码尝试用的格式。
[<RequireQualifiedAccess>]
type ImageEncoding =
    | Png
    | Jpeg of quality: int

/// 决策 332：超大图片自动缩小的几何与命名规则，迁自旧 `Model/ImageShrink.fs`。
/// 解码、摆正、缩放、编码都要 Skia，归宿主；这里只回答「缩到多大、依次试哪些格式、改叫什么名、怎么说」。
/// 最长边不超过 4096 px；有透明通道的 PNG/WebP 先试 PNG，其余（或 PNG 仍太大）转 JPEG，质量 85→75→65，
/// 仍超限再按 0.75 缩边，最多试 8 轮。
module ImageFit =

    let maxDimension = 4096
    let maxAttempts = 8

    /// 最长边超过上限（模型服务会自己缩，白占上传与额度）。
    let tooLarge (width: int, height: int) = max width height > maxDimension

    /// 等比缩到最长边不超过 limit；本来就不超过时原样返回。
    let fit (width: int, height: int) (limit: int) : int * int =
        let longest = max width height

        if longest <= limit then
            width, height
        else
            let scale = float limit / float longest
            max 1 (int (Math.Round(float width * scale))), max 1 (int (Math.Round(float height * scale)))

    /// 这一轮的结果仍超限时，下一轮的最长边上限。
    let nextLimit (width: int, height: int) = int (float (max width height) * 0.75)

    /// 保留透明：只有带透明通道的 PNG / WebP。
    let keepsAlpha (opaque: bool) (mediaType: string) =
        not opaque && (mediaType = "image/png" || mediaType = "image/webp")

    /// 每一轮依次尝试的编码：(格式, 扩展名, 媒体类型)。JPEG 没有透明通道，宿主编码前铺白底。
    let candidates (keepAlpha: bool) : (ImageEncoding * string * string) list =
        [
            if keepAlpha then
                yield ImageEncoding.Png, ".png", "image/png"
            for q in [ 85; 75; 65 ] do
                yield ImageEncoding.Jpeg q, ".jpg", "image/jpeg"
        ]

    /// PNG 编码的质量参数（Skia 对无损格式忽略它，旧代码传 100）。
    let quality (encoding: ImageEncoding) =
        match encoding with
        | ImageEncoding.Png -> 100
        | ImageEncoding.Jpeg q -> q

    /// 换扩展名；文件名没有主干时叫 image。
    let renamed (fileName: string) (extension: string) =
        let stem = Path.GetFileNameWithoutExtension fileName
        (if String.IsNullOrWhiteSpace stem then "image" else stem) + extension

    /// 平台缩小实现（决策 342）只报媒体类型，文件名按它改。
    let renamedFor (fileName: string) (mediaType: string) =
        renamed fileName (if mediaType = "image/png" then ".png" else ".jpg")

    let animatedTooLarge (fileName: string) =
        sprintf "没有添加「%s」：动图超过 8 MiB，缩小会丢掉动画。请先压缩动图，或换成静态图片。" fileName

    let unreadable (fileName: string) =
        sprintf "没有添加「%s」：图片无法读取，可能已损坏。请换一张图片。" fileName

    let unreadableBecause (fileName: string) (reason: string) =
        sprintf "没有添加「%s」：图片无法读取（%s）。请换一张图片。" fileName reason

    let stillTooLarge (fileName: string) =
        sprintf "没有添加「%s」：自动缩小后仍超过 8 MiB。请截取一部分后再添加。" fileName
