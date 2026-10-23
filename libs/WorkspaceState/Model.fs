namespace Wanxiang2.WorkspaceState

open System
open System.Text.Json.Nodes
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Client
open Wanxiang.UI

type ActiveConvState =
    {
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

/// 设置页的事实状态（W7-B）：开合、直达分区与设备分区的全部数据。
/// 单一归属：SettingsVS 的每个字段要么来自这里，要么来自 Conn/Prefs/ServerUrl/Now。
type SettingsPhase =
    {
        Open: bool
        ActiveSection: string option
        Devices: DeviceInfo list
        CanInvite: bool
        Invite: PairingInviteView option
        InviteRejected: string option
        /// 设备列表有界等待的到点时间（None = 不在等待）。与 BoundedWaits.devicesLimit 同值。
        DeviceLoadDue: DateTimeOffset option
        DeviceLoadNote: string option
    }

type OnboardingPhase =
    | Idle
    | Discovering
    | ConnectingProvider of providerId: string * model: string
    | Error of message: string

/// 引导卡的三项在途事实（W7-B3）：归属模型，宿主按单一投影入口回灌给引导卡。
/// 卡片自己的步进机（OnboardingFlow：正在保存/探测/回填）仍是桌内在途态，不进这里。
type OnboardingFacts =
    {
        /// 用户选中的预设键（None = 仍是卡片自己的初始选择）
        SelectedPresetKey: string option
        /// 本机发现的模型清单（None = 还没发现，或本机没有服务）
        DiscoveredModels: string list option
        /// 探测失败后建议改用兼容端点
        SwitchOffered: bool
    }

module OnboardingFacts =
    let empty: OnboardingFacts =
        {
            SelectedPresetKey = None
            DiscoveredModels = None
            SwitchOffered = false
        }

type PairingPhase =
    {
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

type NavigationPhase =
    {
        CompactMode: bool
        DrawerOpen: bool
        SidebarCollapsed: bool
        SidebarWidth: float
    }

type PendingCommand =
    {
        InvocationId: Guid
        Command: ClientCommand
        SentAt: DateTimeOffset
        SuccessMessage: string option
    }

type PendingBatchDeletion =
    {
        Token: Guid
        Ids: Guid list
        Label: string
        DueAt: DateTimeOffset
    }

type FeedbackState =
    {
        PendingCommands: Map<Guid, PendingCommand>
        PendingDeletions: Map<Guid, PendingBatchDeletion>
        PendingConfigs: Map<Guid, string * DateTimeOffset>
    }

type ChromeState =
    {
        Compact: bool
        SidebarWidth: float
        StartingPageVisible: bool
        HeaderEditingDraft: string option
        SearchQuery: string
        SearchActive: bool
        SearchDebouncingUntil: DateTimeOffset option
        SelectedRowIds: Set<Guid>
        BatchMode: bool
        /// 定位目标：对话 id + 消息 id（搜索命中 / 消息链接）。消息加载后由宿主统一 Effect 消费。
        RevealTarget: (Guid * string) option
        RevealTargetExpiresAt: DateTimeOffset option
    }

type ComposerAttachmentDraft =
    {
        Id: Guid
        FileName: string
        MediaType: string
        Size: int64
        Sha256: string
        ProgressPercent: int
        Ready: bool
        Failed: bool
    }

type ComposerState =
    {
        Text: string
        Caret: int option
        EnterSends: bool
        SelectedModel: (string * string) option
        Attachments: ComposerAttachmentDraft list
    }

type ToastItem =
    {
        Id: int
        Text: string
        Tone: ToastTone
        UndoAction: string option
        ExpiresAt: DateTimeOffset
    }

type WorkspaceModel =
    {
        Conn: ConnPhase
        Nav: NavigationPhase
        Overlay: OverlayPhase
        Feedback: FeedbackState
        Prefs: UiPrefs
        Chrome: ChromeState
        Composer: ComposerState
        /// 侧栏一行一个摘要；行数据 = Wanxiang2.ConversationSummary 的解析结果（单一事实类型）。
        Summaries: ConversationSummary list
        /// 分叉谱系：对话 id → 父对话与分叉点（来自同一份对话列表快照，非伪造）
        Lineage: Map<Guid, ForkLineage>
        /// 引导卡的在途事实（所选预设 / 本机发现 / 兼容端点建议）
        Onboarding: OnboardingFacts
        Toasts: ToastItem list
        NextToastId: int
        NextTimerToken: int
        PendingApprovals:
            Map<
                Guid,
                {|
                    ToolName: string
                    CallId: string
                    Summary: string
                    ArgumentsJson: string
                    Policy: string
                    ConversationId: Guid
                |}
             >
        PaletteRecent: string list
        /// 设置页与设备分区状态
        Settings: SettingsPhase
        /// 最近一次发起连接的目标地址（ConnectionRequested 写入；设备邀请与主机条共用）
        ServerUrl: string
        /// 最近一次节拍时刻：设备倒计时与超时的唯一时钟
        Now: DateTimeOffset
        AppliedCursor: uint64
    }
