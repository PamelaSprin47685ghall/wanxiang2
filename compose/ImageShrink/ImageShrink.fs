namespace Wanxiang.UI

open System

// 组合者：图片自动缩小（决策 332）的判定与说明。缩放规则归 ImageFit，媒体类型与 8 MiB 上限归 MediaTypes，大小文案归 MessageView。
// Skia 的解码、缩放、编码仍归宿主；宿主的 `ImageShrink.shrink` / `shrinkAsync` 与本模块同名，F# 按名合并。

/// 一次自动缩小的结果。界面用它告诉用户「原图多大、实际发出去多大」。
type ShrunkImage = {
    bytes: byte[]
    fileName: string
    mediaType: string
    originalBytes: int64
    originalWidth: int
    originalHeight: int
    width: int
    height: int
}

[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module ImageShrink =

    let maxBytes = MediaTypes.maxImageBytes
    let maxDimension = ImageFit.maxDimension

    /// 需要缩小吗？只看模型收的四种图片：超过 8 MiB，或最长边超过 4096 px。
    /// dims 是宿主只读文件头得到的尺寸（已按 EXIF 方向换算）；读不出给 None。
    let needsShrinkFor (fileName: string) (byteCount: int64) (dims: (int * int) option) : bool =
        let mediaType = MediaTypes.ofFileName fileName
        if not (MediaTypes.isModelImage mediaType) then false
        elif byteCount > maxBytes then true
        else dims |> Option.exists ImageFit.tooLarge

    /// 给用户看的一句话：原图 vs 实际发送。
    let note (r: ShrunkImage) =
        sprintf "已自动缩小：原图 %s（%d×%d）→ 发送 %s（%d×%d）"
            (AttachmentRef.formatSize r.originalBytes) r.originalWidth r.originalHeight
            (AttachmentRef.formatSize (int64 r.bytes.Length)) r.width r.height
