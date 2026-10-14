module Tests

// 真实 PendingDeletion + CommandFeedbackTracker；界面面记录事实，计时器是手动时钟。

open System
open Xunit
open Wanxiang.Core
open Wanxiang.UI

let a = Guid "00000000-0000-4000-8000-00000000000a"
let b = Guid "00000000-0000-4000-8000-00000000000b"
let c = Guid "00000000-0000-4000-8000-00000000000c"

type private Rig(active: Guid option) =
    let log = ResizeArray<string>()
    let sent = ResizeArray<ClientCommand>()
    let timers = ResizeArray<TimeSpan * (unit -> unit) * bool ref>()
    let mutable now = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    let mutable active = active
    let mutable undo: (unit -> unit) option = None
    let mutable hidden: Guid list = []
    let commands = CommandFeedbackTracker()
    let titles = Map [ a, "甲"; b, "乙"; c, "丙" ]
    let surface =
        { Now = fun () -> now
          ActiveConversation = fun () -> active
          TitleOf = titles.TryFind
          VisibleOrder = fun () -> [ a; b; c ] |> List.filter (fun id -> not (List.contains id hidden)) |> List.map string
          PendingChanged = fun p ->
              hidden <- p.batches |> List.collect _.ids
              log.Add(sprintf "hidden %d" hidden.Length)
          FocusAfterDelete = fun id -> log.Add("focus-after " + titles[id])
          Open = fun id -> active <- id; log.Add(sprintf "open %s" (id |> Option.map (fun i -> titles[i]) |> Option.defaultValue "-"))
          Render = fun () -> log.Add "render"
          Toast = fun text tone -> log.Add(sprintf "toast %A %s" tone text)
          ToastUndo = fun label act -> undo <- Some act; log.Add("undo-toast " + label)
          After = fun wait act ->
              let cancelled = ref false
              timers.Add(wait, act, cancelled)
              { new IDisposable with member _.Dispose() = cancelled.Value <- true }
          SendCommand = fun cmd -> sent.Add cmd
          NewInvocation = fun () -> Guid.NewGuid() }
    member val Desk = DeletionDesk(commands, surface)
    member _.Commands = commands
    member _.Log = List.ofSeq log
    member _.Sent = List.ofSeq sent
    member _.Undo() = undo.Value ()
    member _.Hidden = hidden
    /// 时间走到 t 之后，触发所有未取消的到期计时
    member _.Advance(by: TimeSpan) =
        now <- now + by
        let live = timers |> Seq.filter (fun (_, _, x) -> not x.Value) |> List.ofSeq
        timers.Clear()
        for _, act, _ in live do act ()
    member _.LiveTimers = timers |> Seq.filter (fun (_, _, x) -> not x.Value) |> Seq.length

// 旧 InteractionReliabilityTests：batch delete reports what it did instead of staying silent
[<Fact>]
let ``batch delete reports what it did instead of staying silent`` () =
    let rig = Rig(None)
    rig.Desk.Schedule [ a; b ]
    Assert.True(rig.Log |> List.exists (fun s -> s.StartsWith "undo-toast" && s.Contains "已删除 2 个对话"), sprintf "%A" rig.Log)
    // 宽限期内命令尚未提交
    Assert.Empty rig.Sent
    Assert.Equal(0, rig.Commands.Count)
    rig.Desk.Commit rig.Desk.Pending.batches.Head.token
    Assert.Equal(2, rig.Sent.Length)
    Assert.Equal(2, rig.Commands.Count)

// 旧 InteractionReliabilityTests：§214 撤销把批量删除的行放回列表且不提交
[<Fact>]
let ``§214 撤销把批量删除的行放回列表且不提交`` () =
    let rig = Rig(None)
    rig.Desk.Schedule [ a ]
    Assert.Equal<Guid list>([ a ], rig.Hidden)
    rig.Undo()
    Assert.Empty rig.Hidden
    Assert.Empty rig.Desk.Pending.batches
    rig.Advance(TimeSpan.FromSeconds 10.0)
    Assert.Empty rig.Sent
    Assert.Contains("toast Success 已恢复对话", rig.Log)

[<Fact>]
let ``删掉当前对话：焦点落邻行、打开邻位（规则归 PendingDeletion.neighborAfter）；撤销单条再打开它`` () =
    let rig = Rig(Some b)
    rig.Desk.Schedule [ b ]
    Assert.Equal<string list>([ "focus-after 乙"; "hidden 1"; "open 甲" ], rig.Log |> List.take 3)
    Assert.Equal("render", List.last rig.Log)
    rig.Undo()
    Assert.Equal("open 乙", rig.Log |> List.filter (fun s -> s.StartsWith "open") |> List.last)

[<Fact>]
let ``宽限期到才提交；只盯最早一批，提交后盯下一批`` () =
    let rig = Rig(None)
    rig.Desk.Schedule [ a ]
    rig.Advance(TimeSpan.FromSeconds 3.0)
    rig.Desk.Schedule [ c ]
    Assert.Equal(1, rig.LiveTimers)
    rig.Advance(TimeSpan.FromSeconds 5.0)
    Assert.Equal<ClientCommand list>([], rig.Sent |> List.filter (function DeleteConversation d -> d.conversationId = c | _ -> false))
    Assert.Single(rig.Sent) |> ignore
    Assert.Equal(1, rig.LiveTimers)
    rig.Advance(TimeSpan.FromSeconds 3.0)
    Assert.Equal(2, rig.Sent.Length)
    Assert.Equal(0, rig.LiveTimers)

[<Fact>]
let ``主机拒绝删除给警告，对话在下一次列表快照里回来`` () =
    let rig = Rig(None)
    rig.Desk.Schedule [ a ]
    rig.Advance(TimeSpan.FromSeconds 8.0)
    let id = match rig.Sent.Head with DeleteConversation d -> d.invocationId | _ -> failwith "?"
    rig.Commands.Reject id |> Option.iter (fun f -> f.onCompleted false)
    Assert.Contains("toast Warning 有对话没删掉：主机拒绝了这次请求，它已回到列表。", rig.Log)

[<Fact>]
let ``同一对话重复删除不重复登记`` () =
    let rig = Rig(None)
    rig.Desk.Schedule [ a ]
    rig.Desk.Schedule [ a ]
    Assert.Single(rig.Desk.Pending.batches) |> ignore
    Assert.True(rig.Desk.IsPending a)
