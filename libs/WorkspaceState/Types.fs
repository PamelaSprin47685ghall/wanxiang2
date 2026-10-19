namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
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
    | DraftWrite of DraftWrite
    | BrowserHook of hookId: string

and Msg =
    | MsgTick of now: DateTimeOffset
    | WireRecv of WireEvent
    | ConnectionUp of instanceId: string
    | ConnectionDown of reason: ConnectionIssue option
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
    | SearchTextChanged of query: string
    | SearchActivate
    | SearchClear
    | SearchHitSelect of convId: Guid * messageId: string option

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
    | Attach of fileName: string * mediaType: string * size: int64 * sha256: string
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
    | FormChanged of sectionKey: string * values: Map<string, string>
    | SaveProvider of requestId: Guid * data: JsonObject
    | DeleteProvider of providerId: string
    | ProbeProvider of providerId: string

and OnboardingUserMsg =
    | StartDiscovery
    | SelectPreset of providerId: string * model: string
    | ConnectDirect of url: string * apiKey: string option
    | CompleteOnboarding

and PaletteUserMsg =
    | Open
    | Close
    | QueryChanged of query: string
    | ExecuteItem of itemId: string

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
