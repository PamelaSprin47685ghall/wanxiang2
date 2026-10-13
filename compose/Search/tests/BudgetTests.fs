module BudgetTests

// 迁自旧仓库 LibraryTests 的预算部分：命中数触顶时明说截断，翻页不丢命中。

open System
open Xunit
open Wanxiang.Core
open Wanxiang.Protocol
open Wanxiang.Server
open Scenario

[<Fact>]
let ``results beyond the hit budget are flagged truncated and pages never silently lose hits`` () =
    let cs =
        [ for n in 1 .. 60 ->
              conv n (sprintf "c%d" n) false false (uint64 n * 10UL)
                  [ for k in 1 .. 5 -> msg (gid n) (uint64 (n * 10 + k)) (text "user" (sprintf "needle %d-%d" n k)) None ] ]
    let proj : Projection<SessionConfig> = { conversations = cs |> List.map (fun c -> c.conversationId, c) |> Map.ofList; latestCommitId = 999UL }
    let collected = ResizeArray<string>()
    let mutable offset = 0
    let mutable go = true
    let mutable last = None
    while go do
        match SearchService.search proj (q "needle" false offset SearchLimits.maxPageSize) with
        | Ok page ->
            for i in page.items do collected.Add(i["snippet"].GetValue<string>())
            offset <- offset + page.items.Count
            go <- page.hasMore
            last <- Some page
        | Error e -> failwith e
    Assert.Equal(SearchLimits.maxHits, collected |> Seq.distinct |> Seq.length)
    Assert.True(last.Value.truncated)
    Assert.False(last.Value.hasMore)
