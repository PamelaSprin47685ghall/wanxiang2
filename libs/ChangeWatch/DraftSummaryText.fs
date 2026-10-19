namespace Wanxiang.UI

/// 设置页「本地草稿」的摘要文案（旧 AppShell.fs 的 UpdateDraftSummary）。
module DraftSummaryText =

    /// drafts：有文字或附件的草稿份数；unsent：未确认的消息条数。
    let text (persistDrafts: bool) (drafts: int) (unsent: int) : string =
        if not persistDrafts then "已关闭：不会在本机保存草稿。"
        elif drafts = 0 && unsent = 0 then "当前没有保存的草稿。"
        else sprintf "已保存 %d 份草稿、%d 条未确认的消息。" drafts unsent
