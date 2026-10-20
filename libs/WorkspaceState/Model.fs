namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

type ActiveConvState = {
    ConversationId: Guid
    Title: string
    Messages: MessageView list
    LastCommitId: uint64
    RuntimeState: string
    Config: SessionConfig
    PageEarliest: uint64
    PageHasMore: bool
    PageLoading: bool
    ParentId: Guid option
}

type ConversationSummaryItem = {
    Id: Guid
    Title: string
    Preview: string
    Running: bool
    Pinned: bool
    Archived: bool
    UpdatedAt: DateTimeOffset
    MessageCount: int
    ProviderId: string
    Model: string
    LastCommitId: uint64
}

type OnboardingPhase =
    | Idle
    | Discovering
    | ConnectingProvider of providerId: string * model: string
    | Error of message: string

type PairingPhase = {
    Code: string option
    ExpiresAt: DateTimeOffset option
    FrozenMinutes: int option
    Error: string option
}

type DialogKind =
    | Confirm of title: string * body: string * confirmAction: Msg
    | Connect of defaultUrl: string
    | TextEdit of title: string * initial: string * confirmLabel: string * targetId: Guid
    | Prompt of title: string * hint: string * initial: string * confirmLabel: string
    | SessionPrefs of config: SessionConfig
    | ShortcutHelp
    | Export of convId: Guid
    | Import
    | SettingsHost

type PopupKind =
    | MessageMenu of messageId: string * anchorId: string
    | HostSwitcher of anchorId: string
    | ToastAnchor of anchorId: string

type OverlayPhase =
    | Closed
    | DrawerOpen
    | DialogOpen of DialogKind
    | PopupOpen of PopupKind

type RunState =
    | Idle of lastError: GenerationError option
    | Generating of generationId: Guid * startedAt: DateTimeOffset * partial: MessageView option

type WorkspacePhase =
    | IdleEmpty
    | LoadingConversation of convId: Guid
    | Active of conv: ActiveConvState * run: RunState

type CatalogPhase =
    | LoadingCatalog
    | NeedsOnboarding of OnboardingPhase
    | Ready of catalog: Catalog * workspace: WorkspacePhase

type ConnPhase =
    | Connecting of attempt: int
    | Pairing of PairingPhase
    | Authed of instanceId: string * catalog: CatalogPhase

type NavigationPhase = {
    CompactMode: bool
    DrawerOpen: bool
    SidebarCollapsed: bool
    SidebarWidth: float
}

type PendingCommand = {
    InvocationId: Guid
    Command: ClientCommand
    SentAt: DateTimeOffset
    SuccessMessage: string option
}

type PendingBatchDeletion = {
    Token: Guid
    Ids: Guid list
    Label: string
    DueAt: DateTimeOffset
}

type FeedbackState = {
    PendingCommands: Map<Guid, PendingCommand>
    PendingDeletions: Map<Guid, PendingBatchDeletion>
    PendingConfigs: Map<Guid, string * DateTimeOffset>
}

type ChromeState = {
    Compact: bool
    SidebarWidth: float
    StartingPageVisible: bool
    HeaderEditingDraft: string option
    SearchQuery: string
    SearchActive: bool
    SearchDebouncingUntil: DateTimeOffset option
    SelectedRowIds: Set<Guid>
    BatchMode: bool
    RevealTarget: string option
    RevealTargetExpiresAt: DateTimeOffset option
}

type ComposerAttachmentDraft = {
    Id: Guid
    FileName: string
    MediaType: string
    Size: int64
    Sha256: string
    ProgressPercent: int
    Ready: bool
    Failed: bool
}

type ComposerState = {
    Text: string
    Caret: int option
    EnterSends: bool
    SelectedModel: (string * string) option
    Attachments: ComposerAttachmentDraft list
}

type ToastItem = {
    Id: int
    Text: string
    Tone: ToastTone
    UndoAction: string option
    ExpiresAt: DateTimeOffset
}

type WorkspaceModel = {
    Conn: ConnPhase
    Nav: NavigationPhase
    Overlay: OverlayPhase
    Feedback: FeedbackState
    Prefs: UiPrefs
    Chrome: ChromeState
    Composer: ComposerState
    Summaries: ConversationSummaryItem list
    Toasts: ToastItem list
    NextToastId: int
    NextTimerToken: int
    PendingApprovals: Map<Guid, {| ToolName: string; CallId: string; Summary: string; ArgumentsJson: string; Policy: string; ConversationId: Guid |}>
    PaletteRecent: string list
}
