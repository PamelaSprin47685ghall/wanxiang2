module JsonPickTests

open System.Text.Json.Nodes
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``values of the right kind are returned, anything else is None`` () =
    let node =
        JsonNode.Parse
            """{"str":"sample","numberInt":123,"big":5000000000,"flag":true,"nestedObj":{"key":"value"},"items":[1,2,3],"nil":null}"""

    Assert.Equal(Some "sample", JsonPick.tryString node "str")
    Assert.Equal(None, JsonPick.tryString node "non_existent")
    Assert.Equal(None, JsonPick.tryString node "nil")
    Assert.Equal(Some 123, JsonPick.tryInt node "numberInt")
    Assert.Equal(None, JsonPick.tryInt node "str")
    Assert.Equal(Some true, JsonPick.tryBool node "flag")
    Assert.Equal(None, JsonPick.tryBool node "str")
    Assert.True((JsonPick.tryObject node "nestedObj").IsSome)
    Assert.Equal(None, JsonPick.tryObject node "flag")
    Assert.True((JsonPick.tryArray node "items").IsSome)
    Assert.Equal(None, JsonPick.tryArray node "str")
    Assert.Equal(None, JsonPick.tryString (JsonNode.Parse "[1]") "str")
