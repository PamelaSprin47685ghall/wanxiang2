namespace Wanxiang.UI

open System.Collections.Generic

// 照抄旧 Rich/Highlight.fs enqueue / notifyOnce 的锁段：Task.Run 与 Task.Delay 去掉，
// enqueue 拆成 TryClaim（进入）与 Finished（finally 之后），notifyOnce 的延时尾段即 NotifyFired。
type HighlightQueue<'K when 'K: equality>() =
    let inFlight = HashSet<'K>()
    let gate = obj ()
    let mutable notifyScheduled = false
    let notifyOnce () =
        let claimed =
            lock gate (fun () ->
                if notifyScheduled then false
                else
                    notifyScheduled <- true
                    true)
        claimed
    static member NotifyWindowMs = 120
    member _.InFlightCount = lock gate (fun () -> inFlight.Count)
    member _.TryClaim(key: 'K) : bool =
        let claimed = lock gate (fun () -> inFlight.Add key)
        claimed
    member _.Finished(key: 'K, ok: bool) : bool =
        lock gate (fun () -> inFlight.Remove key |> ignore)
        if ok then notifyOnce () else false
    member _.NotifyFired() = lock gate (fun () -> notifyScheduled <- false)
