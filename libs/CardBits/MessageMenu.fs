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

/// 生成状态：流式进行中整体没有菜单（互斥事实，替代 streaming 布尔）
[<RequireQualifiedAccess>]
type MenuGeneration =
    | Streaming
    | Settled

/// 消息角色（线上 role 的四档；「助手侧」= 非 user，含 tool 占位消息）
[<RequireQualifiedAccess>]
type MessageRole =
    | User
    | Assistant
    | Tool
    | System

/// 这条消息在对话里的位置：重新生成与复制快捷键只属于最新回复
[<RequireQualifiedAccess>]
type ReplyPosition =
    | LastAssistant
    | Other

/// 「复制消息链接」出口是否装着（无出口不出这一项）
[<RequireQualifiedAccess>]
type LinkSink =
    | Available
    | Absent

/// 右键菜单的全部门控输入（零裸布尔，全由事实 DU 构成）
type MessageMenuFacts =
    {
        generation: MenuGeneration
        role: MessageRole
        position: ReplyPosition
        text: string
        commitId: uint64 option
        linkSink: LinkSink
    }

/// 旧 Views/MessageCard.fs messageMenuEntries 的门控；执行体、图标仍归 MessageCard
module MessageMenu =

    /// 线上 role 字符串归一（空或其它值按助手侧处理，与旧 `not isUser` 同义）
    let roleOfText (role: string) =
        match role with
        | "user" -> MessageRole.User
        | "tool" -> MessageRole.Tool
        | "system" -> MessageRole.System
        | _ -> MessageRole.Assistant

    /// 助手侧（非 user）才有回复类动作
    let private isReplySide (role: MessageRole) = role <> MessageRole.User

    /// 生成中整体为空；其余各项与脚注行 hover 按钮同源
    let entries (facts: MessageMenuFacts) =
        if facts.generation = MenuGeneration.Streaming then
            []
        else
            [
                if not (String.IsNullOrWhiteSpace facts.text) then
                    MessageMenuItem.Copy
                if facts.role = MessageRole.User then
                    MessageMenuItem.EditAndFork
                if isReplySide facts.role && facts.position = ReplyPosition.LastAssistant then
                    MessageMenuItem.Regenerate
                match facts.commitId with
                | Some c when facts.linkSink = LinkSink.Available -> MessageMenuItem.CopyLink c
                | _ -> ()
                match facts.commitId with
                | Some c -> MessageMenuItem.Delete c
                | None -> ()
            ]

    let label =
        function
        | MessageMenuItem.Copy -> "复制消息"
        | MessageMenuItem.EditAndFork -> "编辑并分叉"
        | MessageMenuItem.Regenerate -> "重新生成"
        | MessageMenuItem.CopyLink _ -> "复制消息链接"
        | MessageMenuItem.Delete _ -> "删除这条消息"

    /// 快捷键提示：最新回复的复制与重新生成有全局快捷键
    let hint (facts: MessageMenuFacts) =
        function
        | MessageMenuItem.Copy when isReplySide facts.role && facts.position = ReplyPosition.LastAssistant ->
            "Ctrl+Shift+C"
        | MessageMenuItem.Regenerate -> "Ctrl+Shift+G"
        | _ -> ""

    let isDanger =
        function
        | MessageMenuItem.Delete _ -> true
        | _ -> false
