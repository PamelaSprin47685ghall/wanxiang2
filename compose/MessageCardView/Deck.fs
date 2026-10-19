namespace Wanxiang2.MessageCardView

open Avalonia.Controls
open Wanxiang.UI

/// 消息列表的增量复用（旧 ChatView 的 cardCache + CardReconcile）：同身份的卡原样保留，
/// 只摘掉分叉点之后的、再追加新的；流式卡没有身份，每次重建。
/// 身份类型由组合者定（结构相等即可）；不要每帧把身份格式化成字符串，流式时每帧都要对账。
type CardDeck<'Key when 'Key: equality>(panel: Panel) =
    let reconciler = Reconciler<'Key, Control>()

    /// 摆上一组卡：身份（None = 不复用）与工厂
    member _.Show(desired: ('Key option * (unit -> Control)) list) =
        let plan = reconciler.Reconcile desired
        while panel.Children.Count > plan.Keep do
            panel.Children.RemoveAt(panel.Children.Count - 1)
        for card in plan.Append do
            panel.Children.Add card
        plan

    /// 换对话：清空复用缓存与面板
    member _.Clear() =
        reconciler.Clear()
        panel.Children.Clear()

    member _.CachedCount = reconciler.CachedCount
    member _.Panel = panel

module CardDeck =
    /// 卡片身份：已提交的消息用 commitId 与渲染相关的旗标（最后一条助手、字号）拼成；流式卡没有身份
    let key (message: MessageView) (ctx: CardContext) =
        match message.commitId with
        | Some id when not ctx.Streaming -> Some(sprintf "%d|%b|%g" id ctx.IsLastAssistant ctx.FontSize)
        | _ -> None

    /// 针对 MessageRowVS 的缓存键：commitId 存在且非流式时作为主键
    let keyOfRow (row: Wanxiang2.WorkspaceState.MessageRowVS) (ctx: CardContext) =
        match row.CommitId with
        | Some id when not row.Streaming -> Some(sprintf "%d|%b|%g" id row.IsLastAssistant ctx.FontSize)
        | _ -> None
