namespace Wanxiang2.MessageCardView

open System.Collections.Generic
open Avalonia.Controls
open Wanxiang.Core
open Wanxiang.UI

/// 消息列表的增量复用：同身份的卡原样保留，
/// 只摘掉分叉点之后的、再追加新的；流式卡没有身份，每次重建。
/// 身份类型由组合者定（结构相等即可）；不要每帧把身份格式化成字符串，流式时每帧都要对账。
type CardDeck<'Key when 'Key: equality>(panel: Panel) =
    let cache = Dictionary<'Key, Control>()
    let mutable mounted: 'Key option array = [||]

    /// 摆上一组卡：身份（None = 不复用）与工厂
    member _.Show(desired: ('Key option * (unit -> Control)) list) =
        let wanted = Array.ofList desired
        let mutable common = 0

        while common < mounted.Length
              && common < wanted.Length
              && (match mounted[common], fst wanted[common] with
                  | Some m, Some w -> m = w
                  | _ -> false) do
            common <- common + 1

        while panel.Children.Count > common do
            panel.Children.RemoveAt(panel.Children.Count - 1)

        let _ =
            [
                for index in common .. wanted.Length - 1 do
                    let key, create = wanted[index]

                    let card =
                        match key with
                        | None -> create ()
                        | Some k ->
                            match cache.TryGetValue k with
                            | true, cached -> cached
                            | _ ->
                                let created = create ()
                                cache[k] <- created
                                created

                    panel.Children.Add card
                    card
            ]

        mounted <- wanted |> Array.map fst
        // 缓存只留这一帧用到的身份
        let live = HashSet<'Key>()

        for key in mounted do
            match key with
            | Some k -> live.Add k |> ignore
            | None -> ()

        if cache.Count > live.Count then
            for stale in cache.Keys |> Seq.filter (live.Contains >> not) |> Array.ofSeq do
                cache.Remove stale |> ignore

    /// 换对话：清空复用缓存与面板
    member _.Clear() =
        cache.Clear()
        mounted <- [||]
        panel.Children.Clear()

    member _.CachedCount = cache.Count
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
