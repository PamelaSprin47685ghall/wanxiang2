namespace Wanxiang.UI

/// 旧 AppShell.UpdateDraftSummary 的文案分支照抄
module DraftSummaryText =
    let text (persistDrafts: bool) (texts: int) (unsent: int) =
        if not persistDrafts then "已关闭：不会在本机保存草稿。"
        elif texts = 0 && unsent = 0 then "当前没有保存的草稿。"
        else sprintf "已保存 %d 份草稿、%d 条未确认的消息。" texts unsent
