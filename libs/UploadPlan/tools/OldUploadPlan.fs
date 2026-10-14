namespace Wanxiang.UI

open System
open System.Security.Cryptography

// 原样摘自 AppShell.fs：BeginUploadCore 的 sha 与分块循环（send 换成收集）、BeginUpload 的缩小条件、
// PickAttachment / DropFiles 的归属判断、HandleClipboardPaste 的文件名与分支、各处 toast 文案、
// HandleEvent 的 AttachmentAborted / AttachmentDownloadChunk 文案、SaveDownload 文案。
module UploadPlan =
    let chunkSize = 256 * 1024
    let maxShrinkInput = 64L * 1024L * 1024L
    let sha256Hex (bytes: byte[]) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
    let chunks (bytes: byte[]) : (int * string) list =
        let sent = ResizeArray<int * string>()
        let send (index, data) = sent.Add(index, data)
        let chunkSize = 256 * 1024
        let mutable offset = 0
        let mutable index = 0
        while offset < bytes.Length do
            let count = min chunkSize (bytes.Length - offset)
            send (index, Convert.ToBase64String(bytes, offset, count))
            offset <- offset + count
            index <- index + 1
        List.ofSeq sent
    let shrinkFirst (needsShrink: bool) (byteCount: int64) =
        let bytes = {| Length = byteCount |}
        needsShrink && int64 bytes.Length <= 64L * 1024L * 1024L
    let stillOwned (authenticated: bool) (ownerInstance: string) (ownerConnection: int) (instance: string) (connection: int) =
        let instanceId, client = instance, {| ConnectionGeneration = connection |}
        authenticated && instanceId = ownerInstance && client.ConnectionGeneration = ownerConnection
    let pasteFileName (now: DateTime) =
        let DateTime = {| Now = now |}
        sprintf "paste_%s.png" (DateTime.Now.ToString("yyyyMMdd_HHmmss"))
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
    [<RequireQualifiedAccess>]
    type PasteSource =
        | Files
        | Bitmap
        | NotHandled
    let pasteSource (hasFiles: bool) (hasBitmap: bool) =
        let mutable r = PasteSource.NotHandled
        if hasFiles then
            r <- PasteSource.Files
        elif hasBitmap then
            r <- PasteSource.Bitmap
        else ()
        r
