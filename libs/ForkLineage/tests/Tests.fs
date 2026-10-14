module Tests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

let private row (id: Guid) (fill: JsonObject -> unit) =
    let o = JsonObject()
    o["conversationId"] <- id.ToString("D")
    fill o
    o

[<Fact>]
let ``only rows with a parent enter the table and missing fields stay None`` () =
    let root, fork, old = Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()
    let rows = JsonArray()
    rows.Add(row root ignore)
    rows.Add(row fork (fun o ->
        o["parentId"] <- root.ToString("D")
        o["forkBaseMessageId"] <- 3
        o["forkFirstMessageId"] <- 7UL
        o["parentTitle"] <- "根"
        o["parentDeleted"] <- false
        o["forkOrdinal"] <- 3))
    rows.Add(row old (fun o -> o["parentId"] <- root.ToString("D")))
    rows.Add(JsonValue.Create 1)
    let t = ForkLineage.parse rows
    Assert.False(t.ContainsKey root)
    Assert.Equal({ parentId = root; forkBaseMessageId = Some 3UL; forkFirstMessageId = Some 7UL; parentTitle = Some "根"; parentDeleted = false; forkOrdinal = Some 3 }, t[fork])
    Assert.Equal({ parentId = root; forkBaseMessageId = None; forkFirstMessageId = None; parentTitle = None; parentDeleted = false; forkOrdinal = None }, t[old])

[<Fact>]
let ``negative or non-numeric message ids are ignored`` () =
    let root, fork = Guid.NewGuid(), Guid.NewGuid()
    let rows = JsonArray()
    rows.Add(row fork (fun o -> o["parentId"] <- root.ToString("D"); o["forkBaseMessageId"] <- -1; o["forkFirstMessageId"] <- "7"))
    let l = (ForkLineage.parse rows)[fork]
    Assert.Equal(None, l.forkBaseMessageId)
    Assert.Equal(None, l.forkFirstMessageId)
