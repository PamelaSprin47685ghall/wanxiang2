namespace Wanxiang2.WorkspaceState

open System
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

type TitleMode =
    | Viewing of text: string * CanEdit: bool
    | Editing of Draft: string

type CrumbVS = {
    ParentId: Guid
    ParentTitle: string
    Clickable: bool
}

type ChatHeaderVS = {
    Visible: bool
    Compact: bool
    InsetLeft: float
    Title: TitleMode
    GeneratingStatus: string option
    Crumb: CrumbVS option
    ShowBack: bool
    ShowMore: bool
}

type ShellVS = {
    Compact: bool
    SidebarOpen: bool
    SidebarWidth: float
    StartingPageVisible: bool
}

type SidebarRowVS = {
    Id: Guid
    Title: string
    Preview: string
    Selected: bool
    Pinned: bool
    Archived: bool
    Running: bool
    Busy: bool
    MessageCount: int
}

type SidebarEmptyKind =
    | NotConnected of actionVisible: bool
    | Loading
    | NoConversations
    | NoSearchResults of query: string

type SearchBoxVS = {
    Text: string
    EmptyHintVisible: bool
    Active: bool
    Debouncing: bool
}

type BatchBarVS = {
    SelectedCount: int
    AllSelected: bool
    Busy: bool
    AllPinned: bool
}

type HostSwitcherVS = {
    ServerUrl: string
    Connected: bool
    StatusText: string
    PaletteVisible: bool
}

type SidebarBrandVS = {
    PresetsVisible: bool
}

type SidebarVS = {
    Rows: SidebarRowVS list
    Empty: SidebarEmptyKind
    Search: SearchBoxVS
    Brand: SidebarBrandVS
    Batch: BatchBarVS option
    Host: HostSwitcherVS
    Compact: bool
    BusyRows: Set<Guid>
}

type IdleEmptyKind =
    | NotConnected
    | ConnectionFailed of reason: string
    | NoProvider
    | NoConversation
    | EmptyConversation

type MessageRowVS = {
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
    | Error of reason: string

type ScrollHintVS = {
    CanScrollDown: bool
    CanLoadOlder: bool
}

type CopyButtonState =
    | Idle
    | Fading
    | Confirmed

type CopyButtonVS = {
    State: CopyButtonState
    Text: string
    Tip: string
    Name: string
    ExpiresAt: DateTimeOffset option
}

type ActionStripItemVS = {
    Id: string
    Glyph: string
    Tip: string
    Name: string
    Help: string
    Enabled: bool
}

type ActionStripVS = {
    Items: ActionStripItemVS list
    Subtle: bool
    Visible: bool
}

type AttachmentChipVS = {
    FileName: string
    SizeText: string
    IsImage: bool
    Missing: bool
}

type RevealPaneVS = {
    MaxHeight: float
    Visible: bool
    Expanded: bool
}

type FoldHeaderVS = {
    Title: string
    Duration: string option
    Expanded: bool
    TrailingCopy: bool
}

type StreamCaretVS = {
    Visible: bool
}

type ThinkingBlockVS = {
    Header: FoldHeaderVS
    Body: string
    FontSize: float
    DurationMs: int64 option
}

type ToolCallCardVS = {
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

type ErrorPanelVS = {
    Title: string
    Hint: string
    Detail: string option
    CountdownSeconds: int
    CanContinue: bool
    CanRetry: bool
    RecoveryLabel: string option
}

type UsageFootVS = {
    Timestamp: string option
    TimestampFull: string option
    PromptTokens: int option
    CompletionTokens: int option
    TotalTokens: int option
    DurationMs: int64 option
    CachedTokens: int option
    Detail: string
}

type UserBubbleVS = {
    Text: string
    FontSize: float
    Attachments: AttachmentChipVS list
}

type ChatVS = {
    Phase: ChatPhase
    Streaming: bool
    ScrollHint: ScrollHintVS option
    RevealTarget: string option
}

type SendButtonState =
    | Idle
    | Sending
    | GeneratingStop

type SendButtonVS = {
    State: SendButtonState
    Label: string
}

type AttachmentVS = {
    Id: Guid
    FileName: string
    MediaType: string
    Size: int64
    ProgressPercent: int
    Ready: bool
    Failed: bool
}

type ComposerContextVS = {
    ProviderId: string
    Model: string
    ContextSummary: string
}

type ComposerVS = {
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
}

type DrawerVS = {
    IsOpen: bool
}

type DialogVS = {
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

and ConfirmVS = {
    Body: string
    ConfirmLabel: string
    DangerFirst: bool
    IsPending: bool
}

and ConnectVS = {
    DefaultUrl: string
    IsPairing: bool
    StatusText: string
    StatusVisible: bool
    RetryLabel: string option
    RetryVisible: bool
    CanSubmit: bool
    IsPending: bool
}

and PromptVS = {
    Hint: string
    InitialText: string
    ConfirmLabel: string
    IsPending: bool
    ErrorText: string option
    HelperText: string option
}

and SessionToolVS = {
    Id: string
    Label: string
    Source: string
    Enabled: bool
}

and SessionPrefsVS = {
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

and ShortcutSectionVS = {
    Category: string
    Items: (string * string) list
}

and ShortcutHelpVS = {
    Sections: ShortcutSectionVS list
}

and TextEditVS = {
    Initial: string
    ConfirmLabel: string
    IsPending: bool
}

and ExportVS = {
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

and ImportVS = {
    StatusText: string
    Tone: string
    Details: string list option
    ImportVisible: bool
    ImportEnabled: bool
    PickEnabled: bool
    PickText: string
}

type PopupVS = {
    Kind: PopupKind
    AnchorId: string
}

type OverlayVS =
    | Closed
    | Drawer of DrawerVS
    | Dialog of DialogVS
    | Popup of PopupVS

type SettingsVS = {
    ActiveSection: string
    Catalog: Catalog option
    HasUnsavedChanges: bool
    Scope: string
    ScopeHint: string
    ServerUrl: string
    ProbeResult: (string * bool * string list * string option) option
    Devices: SettingsDevicesVS
    General: SettingsGeneralVS
    Tools: SettingsToolsVS
    Providers: SettingsProvidersVS
}

and SettingsDevicesVS = {
    ServerUrl: string
    CanInvite: bool
    InviteCode: string option
    InviteExpiresAtUtc: DateTimeOffset option
    InviteRejectedReason: string option
    DeviceRows: SettingsDeviceRowVS list
    LoadNote: string option
    EditingDeviceId: string option
    Now: DateTimeOffset
}

and SettingsDeviceRowVS = {
    Id: string
    Name: string
    Title: string
    Meta: string
    Revoked: bool
    CanRename: bool
    CanRevoke: bool
}

and SettingsGeneralVS = {
    Temperature: string
    TopP: string
    MaxTokens: string
    ContextMessages: string
    ToolRounds: string
    Instructions: string
    AutoTitle: bool
    Status: string
    IsPending: bool
    CanSave: bool
    Presets: (string * string * string) list
    ShowImport: bool
    UiPrefs: UiPrefs option
    DraftSummary: string
}

and SettingsToolsVS = {
    ExposeDebug: bool
}

and SettingsProvidersVS = {
    EditingProviderId: string option
}

type OnboardingVS = {
    StepText: string
    DiscoveredLocalModels: string list
    IsBusy: bool
    ErrorMessage: string option
}

type PaletteItemVS = {
    Id: string
    Title: string
    Category: string
}

type PaletteVS = {
    Query: string
    Items: PaletteItemVS list
}

type ModelSwitchOptionVS = {
    ProviderId: string
    Model: string
    DisplayName: string
    IsCurrent: bool
}

type ModelSwitchVS = {
    Filter: string
    Options: ModelSwitchOptionVS list
}

type ApprovalItemVS = {
    ApprovalId: Guid
    ToolName: string
    CallId: string
    Summary: string
    ArgumentsJson: string
    Policy: string
    ConversationId: Guid
}

type ApprovalVS = {
    Items: ApprovalItemVS list
}

type ToastVS = {
    Id: int
    Text: string
    Tone: ToastTone
    UndoAction: string option
    ExpiresAt: DateTimeOffset
}

type WorkspaceViewState = {
    Shell: ShellVS
    Header: ChatHeaderVS
    Sidebar: SidebarVS
    Chat: ChatVS
    Composer: ComposerVS
    Settings: SettingsVS option
    Onboarding: OnboardingVS option
    Overlay: OverlayVS
    Palette: PaletteVS option
    ModelSwitch: ModelSwitchVS option
    Approval: ApprovalVS
    Toasts: ToastVS list
}
