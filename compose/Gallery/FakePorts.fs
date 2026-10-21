namespace Wanxiang2.Gallery

open System
open System.Collections.Generic
open Avalonia.Controls
open Avalonia.Platform.Storage
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI
open Wanxiang2.WorkspaceState
open Wanxiang2.ComposerDesk
open Wanxiang2.ChatScreen
open Wanxiang2.MessageCardView
open Wanxiang2.SidebarDesk
open Wanxiang2.SettingsScreen

/// 画廊的假端口：只给「需要宿主才能动」的那几根线一个确定的实现。
///
/// 这些端口在真实宿主里是网络、剪贴板、文件系统与计时器。画廊不需要它们，
/// 但控件库的入口签名要求它们存在——所以这里给的是**显式的、无副作用的**实现，
/// 每一个都记下「它被调用过」，便于将来断言（例如「删除对话按钮点了会发命令」）。
///
/// 假端口不忽略输入：每一个都读自己的参数（哪怕只是记录），
/// 不做「假装成功但吞掉一切」的桩。见 AGENTS.md《假端口》。
module FakePorts =

    /// 记录下来的调用（按端口分类）。
    type Calls = {
        Sent: ResizeArray<WireEvent>
        Copied: ResizeArray<string>
        Opened: ResizeArray<string>
        Deleted: ResizeArray<Guid>
        Renamed: ResizeArray<string>
        Toasted: ResizeArray<string>
    }

    let newCalls () : Calls =
        { Sent = ResizeArray()
          Copied = ResizeArray()
          Opened = ResizeArray()
          Deleted = ResizeArray()
          Renamed = ResizeArray()
          Toasted = ResizeArray() }

    /// 一次画廊会话共享的假端口。
    type Session() =
        let calls = newCalls ()
        member _.Calls = calls

        /// 输入区的动作：提交只把文本送进模型（不真发命令）。
        member _.ComposerActions() : ComposerDeskActions =
            { Submit = fun text ->
                calls.Toasted.Add(sprintf "submit:%s" text)
                true
              TextChanged = fun _ -> ()
              CaretChanged = fun _ -> ()
              StopGeneration = fun () -> calls.Toasted.Add "stop"
              PickAttachment = fun () -> calls.Toasted.Add "pick-attachment"
              RemoveAttachment = fun id -> calls.Toasted.Add(sprintf "remove-attachment:%O" id)
              OpenModelPicker = fun _ -> calls.Toasted.Add "open-model-picker"
              DropFiles = fun _ -> calls.Toasted.Add "drop-files"
              PasteFromClipboard = fun () -> false
              OpenSessionSettings = fun () -> calls.Toasted.Add "open-session-settings" }

        /// 对话屏的动作。
        member _.ChatActions() : ChatScreenActions =
            { Card =
                { CopyText = fun text -> calls.Copied.Add text
                  Regenerate = fun () -> calls.Toasted.Add "regenerate"
                  EditAndFork = fun m -> calls.Renamed.Add(sprintf "fork:%s" m.text)
                  DeleteMessage = fun id -> calls.Deleted.Add(Guid.Empty) |> ignore; calls.Toasted.Add(sprintf "delete-message:%O" id)
                  DownloadAttachment = fun sha -> calls.Sent.Add(WireEvent.AttachmentDownloadRequest {| sha256 = sha |})
                  OpenLink = fun url -> calls.Opened.Add url
                  CopyLink = Some(fun _ -> calls.Copied.Add "message-link")
                  Recover = None }
              RenameTitle = fun title -> calls.Renamed.Add title
              ToggleSidebar = fun () -> calls.Toasted.Add "toggle-sidebar"
              OpenSessionSettings = fun () -> calls.Toasted.Add "open-session-settings"
              ForkFromHere = fun () -> calls.Toasted.Add "fork"
              ExportConversation = fun () -> calls.Toasted.Add "export"
              StopGeneration = fun () -> calls.Toasted.Add "stop"
              RequestOlderHistory = fun () -> calls.Sent.Add(WireEvent.ObserveConversation {| conversationId = Guid.Empty |})
              FocusHome = fun () -> ()
              OpenParent = fun _ -> ()
              SiblingSelected = fun _ -> ()
              After = fun _ _ -> { new IDisposable with member _.Dispose() = () } }

        /// 侧栏的动作。
        member _.SidebarActions() : SidebarDeskActions =
            { newConversation = fun () -> calls.Toasted.Add "new-conversation"
              openConversation = fun _ -> calls.Toasted.Add "open-conversation"
              renameConversation = fun s -> calls.Renamed.Add s.title
              deleteConversation = fun s -> calls.Deleted.Add s.id
              setPinned = fun s _ -> calls.Toasted.Add(sprintf "set-pinned:%O" s.id)
              setArchived = fun s _ -> calls.Toasted.Add(sprintf "set-archived:%O" s.id)
              selectionAllPinned = fun _ -> false
              setPinnedMany = fun _ _ -> calls.Toasted.Add "set-pinned-many"
              setArchivedMany = fun _ _ -> calls.Toasted.Add "set-archived-many"
              deleteMany = fun ids -> for id in ids do calls.Deleted.Add id
              duplicateAsFork = fun s -> calls.Toasted.Add(sprintf "fork:%O" s.id)
              exportConversation = fun s -> calls.Toasted.Add(sprintf "export:%O" s.id)
              openSettings = fun () -> calls.Toasted.Add "open-settings"
              reconnect = fun () -> calls.Toasted.Add "reconnect"
              toggleArchivedVisibility = fun () -> calls.Toasted.Add "toggle-archived"
              closeNavigation = fun () -> calls.Toasted.Add "close-navigation"
              // 多选：画廊走真状态树（SidebarUserMsg），这里只记录意图，由 Gallery 转成消息。
              enterSelection = fun id -> calls.Toasted.Add(sprintf "enter-selection:%O" id)
              toggleSelected = fun id -> calls.Toasted.Add(sprintf "toggle-selected:%O" id)
              exitSelection = fun () -> calls.Toasted.Add "exit-selection"
              toggleSelectAllVisible = fun ids -> calls.Toasted.Add(sprintf "toggle-select-all:%d" ids.Length) }

        /// 设置页的钩子：写配置改为记一笔，不发网络请求。
        member _.SettingsHooks() : SettingsHooks =
            { UpsertProvider = fun _ onCompleted -> onCompleted true
              DeleteProvider = fun _ -> calls.Toasted.Add "delete-provider"
              ProbeProvider = fun id -> calls.Toasted.Add(sprintf "probe-provider:%s" id)
              UpsertMcp = fun _ onCompleted -> onCompleted true
              DeleteMcp = fun _ -> calls.Toasted.Add "delete-mcp"
              UpdateGeneration = fun _ onCompleted -> onCompleted true
              SavePrefs = fun _ -> ()
              RequestDoctorCheck = fun () -> calls.Toasted.Add "request-doctor-check" }

        /// 卡片的动作。
        member _.CardActions() : CardActions =
            { CopyText = fun text -> calls.Copied.Add text
              Regenerate = fun () -> calls.Toasted.Add "regenerate"
              EditAndFork = fun m -> calls.Renamed.Add(sprintf "fork:%s" m.text)
              DeleteMessage = fun _ -> calls.Deleted.Add Guid.Empty
              DownloadAttachment = fun sha -> calls.Sent.Add(WireEvent.AttachmentDownloadRequest {| sha256 = sha |})
              OpenLink = fun url -> calls.Opened.Add url
              CopyLink = Some(fun _ -> calls.Copied.Add "message-link")
              Recover = None }

    /// 模块级的便捷入口：每次调用新建一份会话。
    let composerActions () = Session().ComposerActions ()
    let chatActions () = Session().ChatActions ()
    let sidebarActions () = Session().SidebarActions ()
    let settingsHooks () = Session().SettingsHooks ()
    let cardActions () = Session().CardActions ()
