module PartialRepliesTests

// 迁自旧仓库 tests/Wanxiang.Tests/PartialPersistAndUrlSchemeTests.fs（决策 250），并补充落盘格式与坏文件。

open System
open System.IO
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server

let partialMessage (text: string) : JsonNode =
    JsonNode.Parse(sprintf """{"role":"assistant","contents":[{"$type":"text","text":%s}]}""" (Text.Json.JsonSerializer.Serialize text))

let tempDir () =
    let d = Path.Combine(Path.GetTempPath(), "wx-partial-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    d

let partial conv = GenerationPartial {| conversationId = conv; generationId = Guid.NewGuid(); message = partialMessage "半截"; error = None |}

[<Fact>]
let ``saved partial survives restart and clear deletes the file`` () =
    let dir = tempDir ()
    let conv = Guid.NewGuid()
    PartialReplyStore(Some dir).Save(partial conv)
    let reopened = PartialReplyStore(Some dir)
    Assert.True((reopened.TryGet conv).IsSome)
    reopened.Clear conv
    Assert.True((PartialReplyStore(Some dir).TryGet conv).IsNone)
    Assert.Empty(Directory.GetFiles(dir, "*.json"))

[<Fact>]
let ``file holds exactly the wire bytes of the event, one per conversation`` () =
    let dir = tempDir ()
    let conv = Guid.NewGuid()
    let store = PartialReplyStore(Some dir)
    store.Save(partial conv)
    let ev = partial conv
    store.Save ev
    Assert.Equal(WireCodec.encode ev, File.ReadAllText(Path.Combine(dir, conv.ToString("D") + ".json")))
    Assert.Single(Directory.GetFiles dir) |> ignore

[<Fact>]
let ``broken files and other events are skipped; memory-only mode works`` () =
    let dir = tempDir ()
    File.WriteAllText(Path.Combine(dir, "x.json"), "{oops")
    File.WriteAllText(Path.Combine(dir, "y.json"), WireCodec.encode Ping)
    let store = PartialReplyStore(Some dir)
    store.Save Ping
    Assert.Equal(2, Directory.GetFiles dir |> Array.length)
    let mem = PartialReplyStore None
    let conv = Guid.NewGuid()
    mem.Save(partial conv)
    Assert.True((mem.TryGet conv).IsSome)
