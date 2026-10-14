namespace Wanxiang.UI

open System.Collections.Generic

/// 一次对账的结果：前 Keep 个已挂项原样留着，其后的全部卸下，再依次挂上 Append
type ReconcilePlan<'V> = { Keep: int; Append: 'V list }

/// 旧 Views/ChatView.fs 卡片对账（1245-1290）：记住已挂的身份序列与按身份缓存的控件。
/// 宿主每次给出想要的序列（身份可缺，缺身份的每次新建），本对象算出共同前缀、
/// 前缀之后按身份取缓存或调工厂新建，并丢掉这一帧不再用到的缓存。面板增删仍归宿主。
type Reconciler<'K, 'V when 'K: equality>() =
    let cache = Dictionary<'K, 'V>()
    let mutable mounted: 'K option array = [||]

    /// 已挂的身份序列（副本）
    member _.MountedKeys = Array.copy mounted

    /// 缓存的条数
    member _.CachedCount = cache.Count

    /// 对账；工厂只在缺身份或缓存未命中时调用
    member _.Reconcile(desired: ('K option * (unit -> 'V)) list) : ReconcilePlan<'V> =
        let wanted = Array.ofList desired
        let mutable common = 0
        while common < mounted.Length
              && common < wanted.Length
              && (match mounted[common], fst wanted[common] with
                  | Some m, Some w -> m = w
                  | _ -> false) do
            common <- common + 1
        let append =
            [ for index in common .. wanted.Length - 1 do
                  let key, create = wanted[index]
                  match key with
                  | None -> create ()
                  | Some k ->
                      match cache.TryGetValue k with
                      | true, cached -> cached
                      | _ ->
                          let created = create ()
                          cache[k] <- created
                          created ]
        mounted <- wanted |> Array.map fst
        // 缓存只留这一帧用到的身份
        let live = HashSet<'K>()
        for key in mounted do
            match key with
            | Some k -> live.Add k |> ignore
            | None -> ()
        if cache.Count > live.Count then
            for stale in cache.Keys |> Seq.filter (live.Contains >> not) |> Array.ofSeq do
                cache.Remove stale |> ignore
        { Keep = common; Append = append }

    /// 换对话等场合：忘掉已挂序列与全部缓存
    member _.Clear() =
        cache.Clear()
        mounted <- [||]
