namespace Wanxiang.UI

open System

/// 侧栏空态三种（泛名，限定访问）
[<RequireQualifiedAccess>]
type SidebarEmptyKind =
    | NotConnected
    | ListLoading
    | NoConversations

/// 侧栏列表为空时的决定
type SidebarEmptyView =
    { /// 搜索无结果提示可见
      searchEmpty: bool
      /// 空态卡可见
      hostVisible: bool
      /// 空态种类；没有空态卡时为 None
      kind: SidebarEmptyKind option
      /// 行动按钮可见（按钮始终挂载，只切可见）
      actionVisible: bool
      skeletonVisible: bool
      /// 卡上的标题（未连接时为 None）
      title: string option
      /// 卡上的说明（加载中时为 None）
      note: string option }

module SidebarEmpty =

    /// 行动按钮文案（只在未连接时出现）
    let actionLabel = "连接主机"

    /// 旧 Sidebar.Rebuild 末段的空态分支
    let decide (query: string) (visibleEmpty: bool) (hitSection: bool) (connected: bool) (listLoading: bool) (compact: bool) =
        let searchEmpty = not (String.IsNullOrWhiteSpace query) && visibleEmpty && not hitSection
        let hostVisible = visibleEmpty && not searchEmpty && not hitSection
        let none =
            { searchEmpty = searchEmpty; hostVisible = hostVisible; kind = None
              actionVisible = false; skeletonVisible = false; title = None; note = None }
        if not (visibleEmpty && not hitSection) then none
        elif not connected then
            // 窄屏抽屉里正文被遮住，侧栏才是唯一入口，按钮才出现
            { none with kind = Some SidebarEmptyKind.NotConnected; actionVisible = compact; note = Some "连接主机后，对话记录会显示在这里。" }
        elif listLoading then
            // 快照未达，没有资格宣判「还没有对话」
            { none with kind = Some SidebarEmptyKind.ListLoading; skeletonVisible = true; title = Some "正在加载对话…" }
        else
            { none with kind = Some SidebarEmptyKind.NoConversations; title = Some "还没有对话"; note = Some "发送第一条消息后，对话会显示在这里。" }
