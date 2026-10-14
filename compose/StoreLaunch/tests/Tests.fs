module Tests

open System
open System.IO
open Xunit
open Wanxiang.Core
open Wanxiang.Store
open Wanxiang.App

let private commit (id: CommitId) =
    Events.Commit.create id DateTimeOffset.UnixEpoch
        [ ConversationCreated { conversationId = Guid.NewGuid(); title = "t"; config = SessionConfig.empty } ]
    |> Events.Commit.withOrigin (Guid.NewGuid()) "wanxiang"

let private withDir f =
    let d = Path.Combine(Path.GetTempPath(), "wx-sl-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    try f d finally (try Directory.Delete(d, true) with _ -> ())

/// 第 1 条完好、中间一帧坏、第 2 条完好：真实的中间损坏。
let private damaged (dir: string) =
    DataPaths.ensureDataDirs dir
    File.WriteAllBytes(DataPaths.logFile dir,
        Array.concat [ Assembly.frame (commit 1UL); JsonSeq.Frame.encode "{not json"; Assembly.frame (commit 2UL) ])

let private logs () =
    let l = ResizeArray<string>()
    l, (fun (s: string) -> lock l (fun () -> l.Add s))

[<Fact>]
let ``空数据目录：正常启动，完整重放`` () =
    withDir (fun d ->
        let l, log = logs ()
        match StoreLaunch.openForStartup d log with
        | Ok s -> s.Shutdown()
        | Error e -> failwith e
        Assert.Equal<string list>([ "no snapshot; full replay" ], List.ofSeq l))

[<Fact>]
let ``损坏日志：只读降级启动并提示 doctor --fix，不截断`` () =
    withDir (fun d ->
        damaged d
        let before = File.ReadAllBytes(DataPaths.logFile d)
        let l, log = logs ()
        match StoreLaunch.openForStartup d log with
        | Ok s -> s.Shutdown()
        | Error e -> failwith e
        Assert.Contains(l, fun m -> m.StartsWith "event log damaged at " && m.Contains "valid through commit 1" && m.EndsWith "Run `wanxiang doctor --fix` to repair.")
        Assert.Equal<byte[]>(before, File.ReadAllBytes(DataPaths.logFile d)))

[<Fact>]
let ``doctor --fix：先建证据包再截断，之后健康`` () =
    withDir (fun d ->
        damaged d
        let l, log = logs ()
        Assert.Equal(Ok(), StoreLaunch.fix d log)
        Assert.Contains(l, fun m -> m.StartsWith "recovery bundle created at ")
        Assert.Contains(l, fun m -> m = sprintf "truncated %s" (DataPaths.logFile d))
        let bundleIdx = l.FindIndex(fun m -> m.StartsWith "recovery bundle")
        let truncIdx = l.FindIndex(fun m -> m.StartsWith "truncated")
        Assert.True(bundleIdx < truncIdx)
        match Inspect.run d with
        | Healthy o -> Assert.Equal(1UL, o.lastCommitId)
        | other -> failwithf "expected healthy, got %A" other)

[<Fact>]
let ``doctor --fix：健康时什么都不做`` () =
    withDir (fun d ->
        let l, log = logs ()
        Assert.Equal(Ok(), StoreLaunch.fix d log)
        Assert.Empty l)

[<Fact>]
let ``数据目录被占用时两条路径都报 locked`` () =
    withDir (fun d ->
        use held = new EventStore(d)
        let _, log = logs ()
        match StoreLaunch.openForStartup d log with
        | Error e -> Assert.StartsWith("data directory locked: ", e)
        | Ok _ -> failwith "expected locked"
        match StoreLaunch.fix d log with
        | Error e -> Assert.StartsWith("data directory locked: ", e)
        | Ok _ -> failwith "expected locked"
        held.Shutdown())
