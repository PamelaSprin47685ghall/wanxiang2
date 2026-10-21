namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 收件箱（迁自旧 Model/DesktopLinks.fs 的 ExternalLinks）：任意线程投递；
/// 订阅者还没来时先排队，订阅时一次交付；之后的投递直接交给订阅者（在投递方线程上，锁外调用）。
type Inbox<'item>() =
    let gate = obj ()
    let queue = Queue<'item>()
    let mutable sink: ('item -> unit) option = None

    member _.Post(item: 'item) =
        let target =
            lock gate (fun () ->
                match sink with
                | Some s -> Some s
                | None ->
                    queue.Enqueue item
                    None)

        target |> Option.iter (fun s -> s item)

    /// 订阅；此前排队的条目在返回前交付（锁外）。
    member _.Subscribe(handler: 'item -> unit) =
        let pending =
            lock gate (fun () ->
                sink <- Some handler
                let items = queue.ToArray()
                queue.Clear()
                items)

        for item in pending do
            handler item

    /// 排队中、尚未交付的条数。
    member _.Pending = lock gate (fun () -> queue.Count)

/// 外部打开的 `wanxiang://` 链接（决策 251）：空白链接不收。
module LinkInbox =
    let accepts (url: string) = not (String.IsNullOrWhiteSpace url)

    /// 空白链接直接丢弃，其余投进收件箱。
    let post (inbox: Inbox<string>) (url: string) =
        if accepts url then
            inbox.Post url
