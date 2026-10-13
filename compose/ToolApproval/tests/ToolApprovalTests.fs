module ToolApprovalTests

// 迁自旧仓库 tests/Wanxiang.Tests/W3ToolPolicyTests.fs 的审批代理部分（端到端部分依赖主机，留在旧仓库）。

open System
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Xunit
open Wanxiang.Config
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server

let private policyConfig (policy: ToolPolicy) (timeoutSeconds: int) : AppConfig =
    let c = AppConfig.defaults (Guid.NewGuid())
    { c with tools = { c.tools with policyDefault = policy; approvalTimeoutSeconds = timeoutSeconds } }

let private newBroker (cfg: AppConfig) (capable: bool) =
    let sent = ResizeArray<Guid * WireEvent>()
    let broker = ToolApprovalBroker((fun () -> cfg), (fun c ev -> lock sent (fun () -> sent.Add((c, ev)))))
    broker.CapabilityProbe <- fun _ -> capable
    broker, sent

let private request (broker: ToolApprovalBroker) (conv: Guid) (ct: CancellationToken) =
    broker.Request(conv, Guid.NewGuid(), "call-1", "builtin_time", "builtin:time", JsonNode.Parse "{}", ct)

let private pendingId (sent: ResizeArray<Guid * WireEvent>) =
    lock sent (fun () ->
        sent |> Seq.pick (fun (_, ev) -> match ev with ToolApprovalRequested r -> Some r.approvalId | _ -> None))

[<Fact>]
let ``[AC-2] 每次询问：挂起等待，期间没有任何人决定就一直不返回；批准后继续`` () =
    let conv = Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskEach 300) true
    Assert.True(broker.NeedsApproval(conv, "builtin:time"))
    let task = request broker conv CancellationToken.None
    Thread.Sleep 400
    Assert.False task.IsCompleted
    Assert.Equal(1, broker.PendingCount)
    broker.Decide(pendingId sent, true, ignore)
    Assert.Equal(Approved, task.Result)
    Assert.Equal(0, broker.PendingCount)
    // 每次询问：批准一次不等于永久放行
    Assert.True(broker.NeedsApproval(conv, "builtin:time"))

[<Fact>]
let ``[AC-2] 首次询问：同一对话里批准一次后不再问，换对话仍然问`` () =
    let conv, other = Guid.NewGuid(), Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskFirst 300) true
    Assert.True(broker.NeedsApproval(conv, "builtin:time"))
    let task = request broker conv CancellationToken.None
    broker.Decide(pendingId sent, true, ignore)
    Assert.Equal(Approved, task.Result)
    Assert.False(broker.NeedsApproval(conv, "builtin:time"))
    Assert.True(broker.NeedsApproval(other, "builtin:time"))
    // 拒绝不被记住
    let broker2, sent2 = newBroker (policyConfig AskFirst 300) true
    let t2 = request broker2 conv CancellationToken.None
    broker2.Decide(pendingId sent2, false, ignore)
    Assert.Equal(Denied(ToolSummary.userDeniedText, true), t2.Result)
    Assert.True(broker2.NeedsApproval(conv, "builtin:time"))

[<Fact>]
let ``[AC-2] 断线重连后待审批请求仍可补发（PendingFor）`` () =
    let conv = Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskEach 300) true
    let task = request broker conv CancellationToken.None
    let id = pendingId sent
    match broker.PendingFor conv with
    | [ ToolApprovalRequested r ] ->
        Assert.Equal(id, r.approvalId)
        Assert.True(r.expiresInSeconds > 0 && r.expiresInSeconds <= 300)
        Assert.Equal("查看当前时间", r.summary)
    | other -> failwithf "应恰有一条待审批：%A" other
    Assert.Empty(broker.PendingFor(Guid.NewGuid()))
    broker.Decide(id, true, ignore)
    task.Wait()
    Assert.Empty(broker.PendingFor conv)

[<Fact>]
let ``[AC-3] 两个客户端同时决定：恰有一个被接受，另一个收到「已处理」`` () =
    for _ in 1..25 do
        let conv = Guid.NewGuid()
        let broker, sent = newBroker (policyConfig AskEach 300) true
        let task = request broker conv CancellationToken.None
        let id = pendingId sent
        let replies = ResizeArray<WireEvent>()
        let reply ev = lock replies (fun () -> replies.Add ev)
        let barrier = new Barrier 2
        let a = Task.Run(fun () -> barrier.SignalAndWait(); broker.Decide(id, true, reply))
        let b = Task.Run(fun () -> barrier.SignalAndWait(); broker.Decide(id, false, reply))
        Task.WaitAll(a, b)
        let outcome = task.Result
        // 恰有一次广播（接受者），恰有一次「已处理」回复（落败者）
        let resolved =
            lock sent (fun () -> sent |> Seq.choose (fun (_, ev) -> match ev with ToolApprovalResolved r -> Some r | _ -> None) |> List.ofSeq)
        Assert.Equal(1, resolved.Length)
        Assert.False resolved.Head.alreadyHandled
        Assert.Equal(1, replies.Count)
        match replies[0] with
        | ToolApprovalResolved r ->
            Assert.True r.alreadyHandled
            Assert.Equal(resolved.Head.status, r.status)
        | other -> failwithf "%A" other
        match outcome with
        | Approved -> Assert.Equal("approved", resolved.Head.status)
        | Denied(_, true) -> Assert.Equal("denied", resolved.Head.status)
        | other -> failwithf "%A" other

[<Fact>]
let ``[AC-5] 重启后挂起的审批消失：旧审批编号得到「已失效」，不会卡死`` () =
    let broker, _ = newBroker (policyConfig AskEach 300) true
    Assert.Equal(0, broker.PendingCount)
    let replies = ResizeArray<WireEvent>()
    broker.Decide(Guid.NewGuid(), true, replies.Add)
    match Seq.toList replies with
    | [ ToolApprovalResolved r ] ->
        Assert.True r.alreadyHandled
        Assert.Equal("expired", r.status)
    | other -> failwithf "%A" other

[<Fact>]
let ``审批能力的观察者在宽限内出现（分叉后立即生成）：照常发起审批而不是秒拒`` () =
    let conv = Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskEach 300) false
    let mutable capable = false
    broker.CapabilityProbe <- fun _ -> capable
    use cts = new CancellationTokenSource()
    let task = request broker conv cts.Token
    Thread.Sleep 300
    capable <- true
    let deadline = DateTime.UtcNow.AddSeconds 3.0
    while DateTime.UtcNow < deadline && (lock sent (fun () -> sent.Count)) = 0 do Thread.Sleep 20
    Assert.False task.IsCompleted
    Assert.Contains(lock sent (fun () -> List.ofSeq sent), fun (_, ev) -> match ev with ToolApprovalRequested _ -> true | _ -> false)
    cts.Cancel()

[<Fact>]
let ``[AC-6] 没有具备审批能力的客户端在线：立即拒绝并给出人话原因，不等待`` () =
    let conv = Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskEach 300) false
    let task = request broker conv CancellationToken.None
    Assert.True(task.Wait 2000)
    match task.Result with
    | Denied(reason, false) ->
        Assert.Contains("没有支持审批的客户端", reason)
        Assert.Contains("拒绝", reason)
    | other -> failwithf "%A" other
    Assert.Equal(0, broker.PendingCount)
    Assert.Empty(lock sent (fun () -> List.ofSeq sent))

[<Fact>]
let ``[AC-6] 审批超时自动拒绝（缺省 300 秒，可配置）`` () =
    Assert.Equal(300, ToolsConfig.defaults.approvalTimeoutSeconds)
    let conv = Guid.NewGuid()
    let broker, sent = newBroker (policyConfig AskEach 1) true
    let sw = Diagnostics.Stopwatch.StartNew()
    let task = request broker conv CancellationToken.None
    Assert.True(task.Wait 10000)
    Assert.True(sw.Elapsed >= TimeSpan.FromMilliseconds 800.0)
    match task.Result with
    | Denied(reason, false) -> Assert.Contains("审批超时", reason)
    | other -> failwithf "%A" other
    let statuses = lock sent (fun () -> sent |> Seq.choose (fun (_, ev) -> match ev with ToolApprovalResolved r -> Some r.status | _ -> None) |> List.ofSeq)
    Assert.Equal<string list>([ "expired" ], statuses)

[<Fact>]
let ``[AC-2] 取消生成时等待中的审批作废`` () =
    let conv = Guid.NewGuid()
    let gen = Guid.NewGuid()
    let broker, _sent = newBroker (policyConfig AskEach 300) true
    use cts = new CancellationTokenSource()
    let task = broker.Request(conv, gen, "c", "builtin_time", "builtin:time", JsonNode.Parse "{}", cts.Token)
    Assert.Equal(1, broker.PendingCount)
    broker.CancelGeneration(conv, Guid.NewGuid()) // 别的生成：不受影响
    Assert.Equal(1, broker.PendingCount)
    broker.CancelGeneration(conv, gen)
    Assert.Equal(ApprovalCancelled, task.Result)
    Assert.Equal(0, broker.PendingCount)
    // 对话被删除同理
    let t2 = broker.Request(conv, gen, "c2", "builtin_time", "builtin:time", JsonNode.Parse "{}", CancellationToken.None)
    broker.ClearConversation conv
    Assert.Equal(ApprovalCancelled, t2.Result)
