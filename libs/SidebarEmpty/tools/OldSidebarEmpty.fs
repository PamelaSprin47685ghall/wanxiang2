namespace Wanxiang.UI

// 照抄旧 Views/Sidebar.fs Rebuild 末段（isSearchEmpty、emptyStateHost.IsVisible、三态分支）；控件写入换成记录字段
open System

[<RequireQualifiedAccess>]
type SidebarEmptyKind =
    | NotConnected
    | ListLoading
    | NoConversations

type SidebarEmptyView =
    { searchEmpty: bool; hostVisible: bool; kind: SidebarEmptyKind option; actionVisible: bool
      skeletonVisible: bool; title: string option; note: string option }

module SidebarEmpty =
    let actionLabel = "连接主机"

    let decide (query: string) (visibleEmpty: bool) (hitSection: bool) (connected: bool) (listLoading: bool) (compactMode: bool) =
        let isSearchEmpty = not (String.IsNullOrWhiteSpace query) && visibleEmpty && not hitSection
        let mutable v =
            { searchEmpty = isSearchEmpty; hostVisible = visibleEmpty && not isSearchEmpty && not hitSection
              kind = None; actionVisible = false; skeletonVisible = false; title = None; note = None }
        if visibleEmpty && not hitSection then
            let variant =
                if not connected then SidebarEmptyKind.NotConnected
                elif listLoading then SidebarEmptyKind.ListLoading
                else SidebarEmptyKind.NoConversations
            v <- { v with kind = Some variant }
            match variant with
            | SidebarEmptyKind.NotConnected ->
                v <- { v with skeletonVisible = false; actionVisible = compactMode; note = Some "连接主机后，对话记录会显示在这里。" }
            | SidebarEmptyKind.ListLoading ->
                v <- { v with skeletonVisible = true; actionVisible = false; title = Some "正在加载对话…" }
            | SidebarEmptyKind.NoConversations ->
                v <- { v with skeletonVisible = false; actionVisible = false; title = Some "还没有对话"; note = Some "发送第一条消息后，对话会显示在这里。" }
        v
