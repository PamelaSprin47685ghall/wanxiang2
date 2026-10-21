module JsonRpcTests

open System.Text.Json.Nodes
open System.Threading
open Xunit
open Wanxiang.Server

[<Fact>]
let ``envelopes omit empty params; notifications have no id`` () =
    Assert.Equal("""{"jsonrpc":"2.0","id":7,"method":"m"}""", (JsonRpc.request 7 "m" (JsonObject())).ToJsonString())
    let p = JsonObject()
    p["a"] <- 1
    Assert.Equal("""{"jsonrpc":"2.0","method":"n","params":{"a":1}}""", (JsonRpc.notification "n" p).ToJsonString())

[<Fact>]
let ``result wins, else error text, else a readable reason`` () =
    Assert.Equal(
        Ok "1",
        JsonRpc.resultOf (JsonNode.Parse """{"result":1}""")
        |> Result.map _.ToJsonString()
    )

    Assert.Equal(Error """{"message":"x"}""", JsonRpc.resultOf (JsonRpc.errorNode "x") |> Result.map _.ToJsonString())

    Assert.Equal(
        Error "MCP 响应既无 result 也无 error",
        JsonRpc.resultOf (JsonNode.Parse "{}") |> Result.map _.ToJsonString()
    )

    Assert.Equal(Error "MCP 响应不是 JSON 对象", JsonRpc.resultOf (JsonNode.Parse "[]") |> Result.map _.ToJsonString())

[<Fact>]
let ``pending calls match by id text; string "1" is not number 1`` () =
    let p = PendingCalls()
    let t = p.Register 1
    Assert.False(p.Complete(JsonNode.Parse("""{"id":"1","result":0}""").AsObject()))
    Assert.True(p.Complete(JsonNode.Parse("""{"id":1,"result":0}""").AsObject()))
    Assert.Equal("0", t.Result["result"].ToJsonString())
    Assert.Equal(0, p.Count)

[<Fact>]
let ``fail all ends every waiter with the same error; timeout and cancel forget the id`` () =
    let p = PendingCalls()
    let a = p.Register 1
    p.FailAll "gone"
    Assert.Contains("gone", string (a.Result["error"]["message"]))
    let b = p.Register 2
    Assert.Contains("超时（0 秒）", string ((p.Await(2, b, 50, CancellationToken.None)).Result["error"]["message"]))
    Assert.Equal(0, p.Count)
    use cts = new CancellationTokenSource()
    cts.Cancel()
    let c = p.Register 3
    Assert.Contains("调用已取消", string ((p.Await(3, c, 0, cts.Token)).Result["error"]["message"]))
    Assert.Equal(0, p.Count)
