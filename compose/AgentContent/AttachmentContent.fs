namespace Wanxiang.Agent

open Microsoft.Extensions.AI

/// 旧 `AttachmentContent`：把 MessageSerde 的附件引用交给 MediaContent 解析。公开面与旧实现相同。
module AttachmentContent =

    let maxInlineTextBytes = MediaContent.maxInlineTextBytes
    let maxInlineImageBytes = MediaContent.maxInlineImageBytes
    let maxInlineBinaryBytes = MediaContent.maxInlineBinaryBytes
    let isImage (mediaType: string) = MediaContent.isImage mediaType
    let isPdf (mediaType: string) = MediaContent.isPdf mediaType
    let isAudio (mediaType: string) = MediaContent.isAudio mediaType
    let isText (mediaType: string) = MediaContent.isText mediaType

    /// 解析单个附件引用。`loadBlob` 返回 None 表示 blob 已丢失。
    let resolve (support: MediaSupport) (loadBlob: string -> byte[] option) (r: AttachmentReference) : AIContent list =
        MediaContent.resolve support loadBlob (r.sha256, r.mediaType, r.fileName, r.size)

    /// 解析一条消息的全部附件，追加到其内容末尾（原顺序）。
    let appendTo
        (support: MediaSupport)
        (loadBlob: string -> byte[] option)
        (refs: AttachmentReference list)
        (msg: ChatMessage)
        : ChatMessage =
        for r in refs do
            for content in resolve support loadBlob r do
                msg.Contents.Add content
        msg
