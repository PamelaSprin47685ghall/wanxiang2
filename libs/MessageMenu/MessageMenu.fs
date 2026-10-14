namespace Wanxiang.UI

open System

/// 消息右键菜单的一项（泛名，限定访问）
[<RequireQualifiedAccess>]
type MessageMenuItem =
    | Copy
    | EditAndFork
    | Regenerate
    | CopyLink of commitId: uint64
    | Delete of commitId: uint64

/// 旧 Views/MessageCard.fs messageMenuEntries 的门控；执行体、图标仍归 MessageCard
module MessageMenu =

    /// 生成中整体为空；其余各项与脚注行 hover 按钮同源
    let entries (streaming: bool) (isUser: bool) (isLastAssistant: bool) (text: string) (commitId: uint64 option) (hasLinkSink: bool) =
        if streaming then []
        else
            [ if not (String.IsNullOrWhiteSpace text) then MessageMenuItem.Copy
              if isUser then MessageMenuItem.EditAndFork
              if not isUser && isLastAssistant then MessageMenuItem.Regenerate
              match commitId with
              | Some c when hasLinkSink -> MessageMenuItem.CopyLink c
              | _ -> ()
              match commitId with
              | Some c -> MessageMenuItem.Delete c
              | None -> () ]

    let label =
        function
        | MessageMenuItem.Copy -> "复制消息"
        | MessageMenuItem.EditAndFork -> "编辑并分叉"
        | MessageMenuItem.Regenerate -> "重新生成"
        | MessageMenuItem.CopyLink _ -> "复制消息链接"
        | MessageMenuItem.Delete _ -> "删除这条消息"

    /// 快捷键提示：最新回复的复制与重新生成有全局快捷键
    let hint (isUser: bool) (isLastAssistant: bool) =
        function
        | MessageMenuItem.Copy when not isUser && isLastAssistant -> "Ctrl+Shift+C"
        | MessageMenuItem.Regenerate -> "Ctrl+Shift+G"
        | _ -> ""

    let isDanger =
        function
        | MessageMenuItem.Delete _ -> true
        | _ -> false
