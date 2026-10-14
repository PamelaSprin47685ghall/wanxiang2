namespace Wanxiang.UI

open System

[<RequireQualifiedAccess>]
type CopyVisual =
    | Default
    | Confirmed
    | Fading

// 照抄旧 Views/MessageCard.fs copyButton（160-230）的计时器结构：
// DispatcherTimer 换成「到期时刻 + 编号」，Tick(now) 按到期先后触发；
// button.Opacity 与图标换成两个字段，形态由它们读出。
type CopyConfirm() =
    let mutable nextId = 0
    let mutable copyTimer: (int * DateTimeOffset) option = None
    let mutable fadeTimer: (int * DateTimeOffset) option = None
    let mutable opacity = 1.0
    let mutable checkIcon = false
    let newTimer (now: DateTimeOffset) (ms: float) =
        nextId <- nextId + 1
        nextId, now.AddMilliseconds ms
    let stopFade () = fadeTimer <- None
    let stopTimer () =
        copyTimer <- None
        stopFade ()
    let pulseCopyConfirmation reduced now =
        if not reduced then
            stopFade ()
            opacity <- 0.6
            fadeTimer <- Some(newTimer now 150.0)
    let restoreDefaultState () =
        stopTimer ()
        opacity <- 1.0
        checkIcon <- false

    static member HoldMs = 1500.0
    static member FadeMs = 150.0

    member _.Copied(now: DateTimeOffset, reduced: bool) =
        stopTimer ()
        pulseCopyConfirmation reduced now
        checkIcon <- true
        copyTimer <- Some(newTimer now 1500.0)

    member _.Tick(now: DateTimeOffset) : CopyVisual =
        // 两只表按到期先后响
        let mutable go = true
        while go do
            let due =
                [ fadeTimer |> Option.map (fun (i, t) -> t, 0, i); copyTimer |> Option.map (fun (i, t) -> t, 1, i) ]
                |> List.choose id |> List.filter (fun (t, _, _) -> t <= now) |> List.sort
            match due with
            | (_, 0, i) :: _ ->
                if fadeTimer |> Option.map fst = Some i then fadeTimer <- None
                opacity <- 1.0
            | (_, _, i) :: _ ->
                if copyTimer |> Option.map fst = Some i then restoreDefaultState ()
            | [] -> go <- false
        if not checkIcon then CopyVisual.Default
        elif opacity < 1.0 then CopyVisual.Fading
        else CopyVisual.Confirmed

    member _.NextDue: DateTimeOffset option =
        match fadeTimer, copyTimer with
        | Some(_, f), _ -> Some f
        | None, c -> c |> Option.map snd

    member _.Reset() = restoreDefaultState ()
    member this.Detached() = this.Reset()
