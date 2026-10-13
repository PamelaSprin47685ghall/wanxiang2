namespace Wanxiang.Server

open System.Collections.Generic

/// 按键缓存长寿对象；对象由「指纹」描述，指纹变了就停掉旧的、换新的，键不再需要就停掉。只依赖 BCL。
/// 所有操作串行，`stop` 在锁内调用。
type RefreshingPool<'V>(stop: 'V -> unit) =

    let items = Dictionary<string, string * 'V>()
    let gate = obj ()

    let drop key =
        match items.TryGetValue key with
        | true, (_, old) ->
            items.Remove key |> ignore
            stop old
        | _ -> ()

    /// `wanted = None`：不再需要，停掉缓存的对象。
    /// `wanted = Some(fingerprint, create)`：指纹相同就复用，否则停旧建新。
    member _.Acquire(key: string, wanted: (string * (unit -> 'V)) option) : 'V option =
        lock gate (fun () ->
            match wanted with
            | None ->
                drop key
                None
            | Some(fp, create) ->
                match items.TryGetValue key with
                | true, (cached, v) when cached = fp -> Some v
                | _ ->
                    drop key
                    let v = create ()
                    items[key] <- (fp, v)
                    Some v)

    /// 停掉 `isLive` 不认的键。
    member _.Prune(isLive: string -> bool) : unit =
        lock gate (fun () ->
            for key in List.ofSeq items.Keys do
                if not (isLive key) then drop key)

    /// 停掉全部（不清空，与旧 ToolRegistry.Dispose 相同）。
    member _.StopAll() : unit =
        lock gate (fun () ->
            for KeyValue(_, (_, v)) in List.ofSeq items do
                stop v)
