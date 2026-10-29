namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI

type FocusTarget =
    | ComposerInput
    | SidebarSearch
    | Row of conversationId: Guid
    | Exchange of messageId: string
    | DialogInitial
    | DrawerToggle
    | Toast
    | None_

[<RequireQualifiedAccess>]
type ScrollCommand =
    | End
    | Beginning
    | ToExchange of messageId: string
    | PreparePrepend
    | RestorePrepend

[<RequireQualifiedAccess>]
type ThemeKind =
    | Light
    | Dark
    | System

[<RequireQualifiedAccess>]
type ToastTone =
    | Neutral
    | Success
    | Warning
    | Failure
    | Info

[<RequireQualifiedAccess>]
type Effect =
    | Focus of FocusTarget
    | ScrollChat of ScrollCommand
    | SendWire of WireEvent
    | SendCommand of ClientCommand
    | After of ms: int * Msg: Msg
    | Every of handle: string * ms: int
    | StopEvery of handle: string
    | ShowToast of text: string * tone: ToastTone
    | Clipboard of text: string
    | OpenLink of url: string
    | SaveDownload of fileName: string * bytes: byte array
    | ApplyTheme of ThemeKind
    | SetWindowBusy of bool
    | CloseWindow
    | PrefsSave of UiPrefs
    | PrefsLoad
    | BrowserHook of hookId: string

and Msg =
    | MsgTick of now: DateTimeOffset
    | WireRecv of WireEvent
    | CommandRequested of ClientCommand
    | ConnectionUp of instanceId: string
    | ConnectionDown of reason: string option
    /// 用户/宿主发起了到某个地址的连接：ServerUrl 的唯一来源（设置页设备分区与侧栏主机条都读它）。
    | ConnectionRequested of url: string
    | TimerFired of token: int
    | EveryFired of handle: string
    | PrefsLoaded of UiPrefs
    | User of UserMsg
    | Internal of InternalMsg

and UserMsg =
    | Sidebar of SidebarUserMsg
    | Chat of ChatUserMsg
    | Composer of ComposerUserMsg
    | Header of HeaderUserMsg
    | Overlay of OverlayUserMsg
    | Settings of SettingsUserMsg
    | Onboarding of OnboardingUserMsg
    | Palette of PaletteUserMsg
    | ModelSwitch of ModelSwitchUserMsg
    | Transfer of TransferUserMsg
    | Devices of DeviceUserMsg

and SidebarUserMsg =
    | SelectConv of convId: Guid
    | NewConversation
    | RenameConversation of convId: Guid * newTitle: string
    | TogglePin of convId: Guid
    | DeleteConversation of convId: Guid
    | BatchDelete of convIds: Guid list
    /// 进入多选：把第一项选上（已在多选中则只并入这一项）
    | EnterBatch of convId: Guid
    /// 切换一行的选中态；清空最后一项时退出多选
    | ToggleBatch of convId: Guid
    /// 退出多选（清空选择）
    | ExitBatch
    /// 全选可见行；已全选则退出多选
    | ToggleBatchAllVisible of visibleIds: Guid list
    | SearchTextChanged of query: string
    | SearchActivate
    | SearchClear
    | SearchHitSelect of convId: Guid * messageId: string option
    /// 归档可见性切换（偏好的一部分：模型改一次、落盘一次，投影随之更新）
    | ToggleShowArchived

and ChatUserMsg =
    | JumpBeginning
    | JumpEnd
    | Regenerate
    | EditFork of commitId: uint64 option * seedText: string
    | DeleteExchange of messageId: string
    | SwitchVersion of convId: Guid
    | ContinueReply
    | ApprovalRespond of approvalId: Guid * approved: bool
    | CopyLatestReply
    | OlderPageRequested

and ComposerUserMsg =
    | TextChanged of text: string
    | CaretChanged of caret: int option
    | Send
    | StopGeneration
    /// 挂一个附件。`attachmentId` 由**发起方**给（客户端在开始上传时生成）——
    /// 它同时是这次上传的身份：状态树用它对上 `UploadProgress` / `UploadFinished` / `UploadFailed`，
    /// 线协议也用它（`attachment.begin` / `chunk` / `complete`）。两侧必须是同一个 id。
    | Attach of attachmentId: Guid * fileName: string * mediaType: string * size: int64 * sha256: string
    | RemoveAttachment of attachmentId: Guid
    | ModelPick of providerId: string * model: string
    | EnterSendsToggled of bool

and HeaderUserMsg =
    | Back
    | ToggleSidebar
    | StartRename
    | CommitRename of newTitle: string
    | CancelRename
    | CrumbClick of parentId: Guid

and OverlayUserMsg =
    | Escape
    | Dismiss
    | PopupOpened of popupId: string
    | PopupClosed of popupId: string
    | ToastUndo of actionId: string
    | ToastDismiss of toastId: int

and SettingsUserMsg =
    | OpenSection of sectionKey: string option
    | Close
    /// 本机偏好已改（外观/归档可见性等）：模型改一次、落盘一次，投影随下次渲染更新各分区
    | PrefsChanged of prefs: UiPrefs

/// 引导卡回给宿主的在途事实（W7-B3）：卡片不自己留副本，事实进模型，宿主投影回来。
and OnboardingUserMsg =
    /// 用户在卡片上选了预设（预设键，如 openai / ollama / custom）
    | SelectPreset of key: string
    /// 本机发现的结果（None = 没有本机服务或还没发现）
    | Discovered of models: string list option
    /// 探测失败后建议改用兼容端点
    | SwitchOffered of flag: bool

and PaletteUserMsg =
    | Open
    | Close
    | QueryChanged of query: string
    | ExecuteItem of itemId: string
    | PushRecent of itemId: string
    | SetRecent of recent: string list

and ModelSwitchUserMsg =
    | Open
    | Close
    | FilterChanged of query: string
    | SelectModel of providerId: string * model: string

and TransferUserMsg =
    | ExportConversation of convId: Guid
    | ImportPreviewRequest of jsonText: string
    | ImportCommit of jsonText: string
    | CloseTransfer

and DeviceUserMsg =
    | LoadDevices
    | RequestInvite
    | RenameDevice of deviceId: string * newName: string
    | RevokeDevice of deviceId: string

and InternalMsg =
    | UploadProgress of attachmentId: Guid * percent: int
    | UploadFinished of attachmentId: Guid
    | UploadFailed of attachmentId: Guid * reason: string
    | ProbeResult of requestId: Guid * ok: bool * error: string option
    | HostHealthTick of ok: bool
    | OlderPageTimeout of convId: Guid
    | DebounceSearchFired of query: string
    | RevealTargetExpired
