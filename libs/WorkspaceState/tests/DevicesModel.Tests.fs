module DevicesModelFactsTests

open System
open System.Text.Json.Nodes
open Xunit
open Wanxiang.UI

[<Fact>]
let ``countdown rounds up and never goes negative`` () =
    Assert.Equal("0:00", DevicesModel.countdown (TimeSpan.FromSeconds -3.0))
    Assert.Equal("0:01", DevicesModel.countdown (TimeSpan.FromMilliseconds 1.0))
    Assert.Equal("5:00", DevicesModel.countdown DevicesModel.inviteLifetime)

[<Fact>]
let ``only six digit codes are grouped`` () =
    Assert.Equal("123 456", DevicesModel.groupCode "123456")
    Assert.Equal("1234567", DevicesModel.groupCode "1234567")

[<Fact>]
let ``malformed device entries are skipped or defaulted`` () =
    let parsed =
        DevicesModel.parse (JsonNode.Parse("""[1, {"id":"x","revoked":"yes"}]""").AsArray())

    match parsed with
    | [ d ] ->
        Assert.Equal("x", d.id)
        Assert.False d.revoked
        Assert.Equal(None, d.lastSeen)
    | other -> failwithf "%A" other

[<Fact>]
let ``revoked wins over online in the last seen text`` () =
    let d =
        {
            id = "a"
            name = ""
            createdAt = None
            lastSeen = None
            revoked = true
            online = true
            current = false
            desktop = false
        }

    Assert.Equal("已吊销", DevicesModel.lastSeenText DateTimeOffset.UtcNow d)
    Assert.Equal("在线", DevicesModel.lastSeenText DateTimeOffset.UtcNow { d with revoked = false })

    Assert.Equal(
        "从未连接",
        DevicesModel.lastSeenText
            DateTimeOffset.UtcNow
            { d with
                revoked = false
                online = false
            }
    )
