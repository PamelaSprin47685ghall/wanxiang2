namespace Wanxiang2.WorkspaceState

open System
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.UI

type TitleMode =
    | Viewing of text: string * CanEdit: bool
    | Editing of Draft: string

type CrumbVS =
    {
        ParentId: Guid
        ParentTitle: string
        Clickable: bool
    }

type ChatHeaderVS =
    {
        Visible: bool
        Compact: bool
        InsetLeft: float
        Title: TitleMode
        GeneratingStatus: string option
        Crumb: CrumbVS option
        ShowBack: bool
        ShowMore: bool
    }

type ShellVS =
    {
        Compact: bool
        SidebarOpen: bool
        SidebarWidth: float
        StartingPageVisible: bool
    }

type SidebarRowVS =
    {
        Id: Guid
        Title: string
        Preview: string
        Pinned: bool
        Archived: bool
        Running: bool
        MessageCount: int
        CreatedAt: DateTimeOffset
        UpdatedAt: DateTimeOffset
        IsFork: bool
        ProviderId: string
        Model: string
        LastCommitId: uint64
        Busy: bool
    }

type SidebarEmptyKind =
    | NotConnected of actionVisible: bool
    | Loading
    | NoConversations
    | NoSearchResults of query: string

type SearchBoxVS =
    {
        Text: string
        EmptyHintVisible: bool
        Active: bool
        Debouncing: bool
    }

type BatchBarVS =
    {
        SelectedCount: int
        AllSelected: bool
        Busy: bool
        AllPinned: bool
    }

type HostSwitcherVS =
    {
        ServerUrl: string
        Connected: bool
        StatusText: string
    }

type SidebarBrandVS =
    {
        PresetsVisible: bool
        /// 紧凑态：品牌行出现返回键
        Compact: bool
    }

type SidebarVS =
    {
        Rows: SidebarRowVS list
        Empty: SidebarEmptyKind
        Search: SearchBoxVS
        Brand: SidebarBrandVS
        /// 是否处于多选模式（同源于 `Chrome.BatchMode`）。
        BatchMode: bool
        /// 多选选中的对话 id（**多选的唯一事实**）。侧栏只读它，不再自己记一份选择集合。
        SelectedRowIds: Set<Guid>
        Host: HostSwitcherVS
        Compact: bool
        BusyRows: Set<Guid>
        ActiveId: Guid option
        ShowArchived: bool
        Lineage: Map<Guid, Wanxiang.UI.ForkLineage>
    }

type IdleEmptyKind =
    | NotConnected
    | ConnectionFailed of reason: string
    | NoProvider
    | NoConversation
    | EmptyConversation

type MessageRowVS =
    {
        Role: string
        Text: string
        Reasoning: string
        CommitId: uint64 option
        ToolCalls: ToolCallView list
        Attachments: AttachmentRef list
        CommittedAt: DateTimeOffset option
        Usage: GenerationUsage option
        IsLastAssistant: bool
        Streaming: bool
        AutoCollapseReasoning: bool
        MissingAttachments: Set<string>
        AwaitingApproval: Set<string>
        Index: int option
    }

type ChatPhase =
    | Skeleton
    | Empty of IdleEmptyKind
    | Messages of MessageRowVS list

type ScrollHintVS =
    {
        CanScrollDown: bool
        CanLoadOlder: bool
    }

type CopyButtonState =
    | Idle
    | Fading
    | Confirmed

type CopyButtonVS =
    {
        State: CopyButtonState
        Text: string
        Tip: string
        Name: string
        ExpiresAt: DateTimeOffset option
    }

type ActionStripItemVS =
    {
        Id: string
        Glyph: string
        Tip: string
        Name: string
        Help: string
        Enabled: bool
    }

type ActionStripVS =
    {
        Items: ActionStripItemVS list
        Subtle: bool
        Visible: bool
    }

type AttachmentChipVS =
    {
        FileName: string
        SizeText: string
        IsImage: bool
        Missing: bool
    }

type RevealPaneVS =
    {
        MaxHeight: float
        Visible: bool
        Expanded: bool
    }

type FoldHeaderVS =
    {
        Title: string
        Duration: string option
        Expanded: bool
        TrailingCopy: bool
    }

type StreamCaretVS = { Visible: bool }

type ThinkingBlockVS =
    {
        Header: FoldHeaderVS
        Body: string
        FontSize: float
        DurationMs: int64 option
    }

type ToolCallCardVS =
    {
        CallName: string
        DisplayName: string
        Tone: string
        Badge: string
        ArgCount: int
        Preview: string
        HumanSummary: string
        ArgumentsJson: string
        Result: string option
        Error: string option
        Expanded: bool
    }

/// 错误卡的动作行成员
type ErrorPanelAction =
    | Continue
    | Retry
    | Recovery of label: string

type ErrorPanelVS =
    {
        Title: string
        Hint: string
        Detail: string option
        /// 剩余秒数；-1 表示没有倒计时
        CountdownSeconds: int
        /// 倒计时是否还在走（组合者据此驱动 Tick）
        Ticking: bool
        /// 本帧是否播报「可以重试了」（只在第一次走完时为真）
        Announce: bool
        /// 动作行（顺序即显示顺序）
        Actions: ErrorPanelAction list
        /// 「重试」动作
        OnRetry: unit -> unit
        /// 「继续生成」动作；None 时不出这个按钮
        OnContinue: (unit -> unit) option
        /// 恢复动作（打开设置等）；None 时不出恢复按钮。参数是按钮上的标签。
        OnRecovery: (string -> unit) option
    }

type UsageFootVS =
    {
        Timestamp: string option
        TimestampFull: string option
        PromptTokens: int option
        CompletionTokens: int option
        TotalTokens: int option
        DurationMs: int64 option
        CachedTokens: int option
        Detail: string
    }

type UserBubbleVS =
    {
        Text: string
        FontSize: float
        Attachments: AttachmentChipVS list
    }

type ChatVS =
    {
        Phase: ChatPhase
        Streaming: bool
        ScrollHint: ScrollHintVS option
        RevealTarget: string option
        /// 生成错误（来自 `RunState.Idle (Some err)`）。
        ///
        /// 这是**生成**错误（生成失败、可重试），与 `ChatPhase.Error` 那个分支**不是同一件事**：
        /// 后者原本用来画「加载更早消息失败」的横幅，无生产者，是死分支（见《已知缺口》）。
        /// 错误卡（`MessageCardView.Card.ErrorCard`）要拿到的是本字段。
        Error: GenerationError option
    }

type SendButtonState =
    | Idle
    | Sending
    | GeneratingStop

type SendButtonVS =
    {
        State: SendButtonState
        Label: string
    }

type AttachmentVS =
    {
        Id: Guid
        FileName: string
        MediaType: string
        Size: int64
        ProgressPercent: int
        Ready: bool
        Failed: bool
    }

type ComposerContextVS =
    {
        ProviderId: string
        Model: string
        ContextSummary: string
    }

type ComposerVS =
    {
        Enabled: bool
        DisabledReason: string option
        Text: string
        Caret: int option
        EnterSends: bool
        Send: SendButtonVS
        Tray: AttachmentVS list
        ModelChip: string option
        Context: ComposerContextVS option
        Compact: bool
        /// 决策 320/330/331：当前所选模型能否看图（用户指定 > 模型服务信息 > 名字推断），发送门控的直接输入。
        Vision: VisionSupport
        /// 受阻提示里念的模型展示名；VisionUnknown/VisionYes 时为空串。
        VisionLabel: string
    }

type DrawerVS = { IsOpen: bool }

type DialogVS =
    {
        Kind: DialogKind
        Title: string
        CanDismiss: bool
        Confirm: ConfirmVS option
        Connect: ConnectVS option
        Prompt: PromptVS option
        SessionPrefs: SessionPrefsVS option
        ShortcutHelp: ShortcutHelpVS option
        TextEdit: TextEditVS option
        Export: ExportVS option
        Import: ImportVS option
    }

and ConfirmVS =
    {
        Body: string
        ConfirmLabel: string
        DangerFirst: bool
        IsPending: bool
    }

and ConnectVS =
    {
        DefaultUrl: string
        IsPairing: bool
        StatusText: string
        StatusVisible: bool
        RetryLabel: string option
        RetryVisible: bool
        CanSubmit: bool
        IsPending: bool
    }

and PromptVS =
    {
        Hint: string
        InitialText: string
        ConfirmLabel: string
        IsPending: bool
        ErrorText: string option
        HelperText: string option
    }

and SessionToolVS =
    {
        Id: string
        Label: string
        Source: string
        Enabled: bool
    }

and SessionPrefsVS =
    {
        Instructions: string
        Temperature: string
        TopP: string
        MaxTokens: string
        ThinkingBudget: string
        Tools: SessionToolVS list
        Customized: bool
        SummaryError: string option
        IsPending: bool
    }

and ShortcutSectionVS =
    {
        Category: string
        Items: (string * string) list
    }

and ShortcutHelpVS = { Sections: ShortcutSectionVS list }

and TextEditVS =
    {
        Initial: string
        ConfirmLabel: string
        IsPending: bool
    }

and ExportVS =
    {
        StatusText: string
        Tone: string
        ProgressVisible: bool
        Indeterminate: bool
        ProgressValue: float
        RetryVisible: bool
        SaveVisible: bool
        SaveJsonVisible: bool
        SaveEnabled: bool
        SaveJsonEnabled: bool
        CloseText: string
    }

and ImportVS =
    {
        StatusText: string
        Tone: string
        Details: string list option
        ImportVisible: bool
        ImportEnabled: bool
        PickEnabled: bool
        PickText: string
    }

type PopupVS = { Kind: PopupKind; AnchorId: string }

type OverlayVS =
    | Closed
    | Drawer of DrawerVS
    | Dialog of DialogVS
    | Popup of PopupVS

/// 设置页的唯一视图状态归属（W7-B）：AppShell→SettingsScreen.Render 的单一输入。
/// 分区键是字符串（palette/线协议概念）；SettingsPart 是设置页自己的导航概念，由 SettingsScreen 换算。
type SettingsVS =
    {
        /// 宿主直达的分区键；None = 保持设置页当前分区
        ActiveSection: string option
        Catalog: Catalog option
        InstanceId: string
        ServerUrl: string
        Prefs: UiPrefs
        Devices: SettingsDevicesVS
    }

/// 设备分区的投影；行数据与邀请直接用 DevicesModel 的事实类型（Editing 内联改名是设置桌瞬态，不在此列）
and SettingsDevicesVS =
    {
        ServerUrl: string
        Devices: DeviceInfo list
        CanInvite: bool
        Invite: PairingInviteView option
        InviteRejected: string option
        LoadNote: string option
        IsLoading: bool
        Now: DateTimeOffset
    }

type PaletteItemVS =
    {
        Id: string
        Title: string
        Category: string
    }

type PaletteVS =
    {
        Query: string
        Items: PaletteItemVS list
    }

type ModelSwitchOptionVS =
    {
        ProviderId: string
        Model: string
        DisplayName: string
        IsCurrent: bool
    }

type ModelSwitchVS =
    {
        Filter: string
        Options: ModelSwitchOptionVS list
    }

type ApprovalItemVS =
    {
        ApprovalId: Guid
        ToolName: string
        CallId: string
        Summary: string
        ArgumentsJson: string
        Policy: string
        ConversationId: Guid
    }

type ApprovalVS = { Items: ApprovalItemVS list }

type ToastVS =
    {
        Id: int
        Text: string
        Tone: ToastTone
        UndoAction: string option
        ExpiresAt: DateTimeOffset
    }

type WorkspaceViewState =
    {
        Shell: ShellVS
        Header: ChatHeaderVS
        Sidebar: SidebarVS
        Chat: ChatVS
        Composer: ComposerVS
        Settings: SettingsVS option
        Overlay: OverlayVS
        Palette: PaletteVS option
        ModelSwitch: ModelSwitchVS option
        Approval: ApprovalVS
        Toasts: ToastVS list
    }
