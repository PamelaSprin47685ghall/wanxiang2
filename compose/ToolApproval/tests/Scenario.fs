module Scenario

// 金样场景：审批代理广播和回复的线上字节（审批编号随机，统一替换成 <id>）。
// 同一份源码既编进新组合者的测试，也编进旧 ToolApproval.fs 生成金样。

open System
open System.Text.Json.Nodes
open System.Threading
open Wanxiang.Config
open Wanxiang.Protocol
open Wanxiang.Server

let conv = Guid("11111111-2222-3333-4444-555555555555")
let gen = Guid("66666666-7777-8888-9999-000000000000")

let all () =
    let out = ResizeArray<string>()
    let ids = ResizeArray<Guid>()
    let text (ev: WireEvent) =
        let mutable s = WireCodec.encode ev
        for i in 0 .. ids.Count - 1 do s <- s.Replace(ids[i].ToString("D"), sprintf "<id%d>" i)
        s
    let record tag ev =
        (match ev with ToolApprovalRequested r -> lock ids (fun () -> ids.Add r.approvalId) | _ -> ())
        lock out (fun () -> out.Add(tag + " " + text ev))
    let cfg policy seconds =
        let c = AppConfig.defaults (Guid("00000000-0000-0000-0000-000000000001"))
        { c with tools = { c.tools with policyDefault = policy; approvalTimeoutSeconds = seconds } }
    let broker (c: AppConfig) capable =
        let b = ToolApprovalBroker((fun () -> c), (fun cid ev -> record (sprintf "broadcast %O" cid) ev))
        b.CapabilityProbe <- (fun _ -> capable)
        b.CapabilityGraceMs <- 0
        b
    let last () = lock ids (fun () -> ids[ids.Count - 1])
    let outcome (t: Threading.Tasks.Task<ApprovalOutcome>) = out.Add(sprintf "outcome %A" t.Result)

    let b = broker (cfg AskFirst 300) true
    out.Add(sprintf "needs %b" (b.NeedsApproval(conv, "builtin:time")))
    let t = b.Request(conv, gen, "call-1", "builtin_time", "builtin:time", JsonNode.Parse """{"q":"好"}""", CancellationToken.None)
    for ev in b.PendingFor conv do
        // 剩余秒数随时间变化，只比较格式
        out.Add("pending " + Text.RegularExpressions.Regex.Replace(text ev, "\"expiresInSeconds\":\\d+", "\"expiresInSeconds\":<left>"))
    let id = last ()
    b.Decide(id, true, record "reply")
    outcome t
    b.Decide(id, false, record "reply")
    out.Add(sprintf "needs %b" (b.NeedsApproval(conv, "builtin:time")))
    b.Decide(Guid.Empty, true, record "reply")

    let b2 = broker (cfg AskEach 300) true
    let t2 = b2.Request(conv, gen, "call-2", "search", "mcp:s/search", JsonNode.Parse """{"query":"x"}""", CancellationToken.None)
    b2.Decide(last (), false, record "reply")
    outcome t2
    let t3 = b2.Request(conv, gen, "call-3", "search", "mcp:s/search", null, CancellationToken.None)
    b2.CancelGeneration(conv, gen)
    outcome t3
    b2.Decide(last (), true, record "reply")
    let t4 = b2.Request(conv, gen, "call-4", "x", "x", JsonNode.Parse "{}", CancellationToken.None)
    b2.ClearConversation conv
    outcome t4

    let b3 = broker (cfg AskEach 120) false
    outcome (b3.Request(conv, gen, "call-5", "x", "x", JsonNode.Parse "{}", CancellationToken.None))

    let b4 = broker (cfg AskEach 1) true
    outcome (b4.Request(conv, gen, "call-6", "x", "x", JsonNode.Parse "{}", CancellationToken.None))
    String.concat "\n" out
