namespace Wanxiang.UI

open System
open System.Security.Cryptography

/// 附件上传与取回的纯部分（旧 AppShell 的 PickAttachment / DropFiles / HandleClipboardPaste / BeginUpload / BeginUploadCore / SaveDownload）。
/// 读文件、剪贴板、Skia 缩小、发线上消息、存文件都归宿主；这里给出分块、校验和、何时先缩小、上传归属判断与提示文案。
module UploadPlan =

    /// 每块原始字节数（base64 前）。
    let chunkSize = 256 * 1024

    /// 超过这个大小的文件不尝试自动缩小，直接走校验（由 MediaTypes 拒绝）。
    let maxShrinkInput = 64L * 1024L * 1024L

    /// 小写十六进制 SHA-256。
    let sha256Hex (bytes: byte[]) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()

    /// 依次发送的块：(序号, base64)。空文件没有块。
    let chunks (bytes: byte[]) : (int * string) list =
        [ let mutable offset = 0
          let mutable index = 0
          while offset < bytes.Length do
              let count = min chunkSize (bytes.Length - offset)
              yield index, Convert.ToBase64String(bytes, offset, count)
              offset <- offset + count
              index <- index + 1 ]

    /// 决策 332：需要缩小（ImageShrink.needsShrink 的结果）且不超过 64 MiB 时先在后台缩小。
    let shrinkFirst (needsShrink: bool) (byteCount: int64) = needsShrink && byteCount <= maxShrinkInput

    /// 读完文件回到界面线程时，上传仍归属发起时的那次连接：已认证、同一主机、同一代连接。
    let stillOwned (authenticated: bool) (ownerInstance: string) (ownerConnection: int) (instance: string) (connection: int) =
        authenticated && instance = ownerInstance && connection = ownerConnection

    /// 剪贴板图片的文件名（本地时间）。
    let pasteFileName (now: DateTime) = sprintf "paste_%s.png" (now.ToString("yyyyMMdd_HHmmss"))

    let disconnectedText = "连接已断开，请重新选择附件；文件没有发送到其他主机。"
    let shrinkingText (fileName: string) = sprintf "「%s」太大，正在自动缩小…" fileName
    let shrunkText (fileName: string) (note: string) = sprintf "「%s」%s" fileName note
    let readFailedText (reason: string) = sprintf "读取文件失败：%s" reason
    let dropReadFailedText (reason: string) = sprintf "读取拖入文件失败：%s" reason
    let clipboardFilesFailedText (reason: string) = sprintf "读取剪贴板文件失败：%s" reason
    let clipboardImageFailedText (reason: string) = sprintf "读取剪贴板图片失败：%s" reason
    let savedText (fileName: string) = sprintf "已保存 %s" fileName
    let saveFailedText (reason: string) = sprintf "保存失败：%s" reason
    let uploadFailedText (fileName: string) = sprintf "上传失败：%s" fileName
    let downloadCappedText = "附件过大，下载已中止。"

    /// 剪贴板里有什么就走哪条路：文件优先，其次位图，都没有就不接管粘贴。
    [<RequireQualifiedAccess>]
    type PasteSource =
        | Files
        | Bitmap
        | NotHandled

    let pasteSource (hasFiles: bool) (hasBitmap: bool) =
        if hasFiles then PasteSource.Files elif hasBitmap then PasteSource.Bitmap else PasteSource.NotHandled
