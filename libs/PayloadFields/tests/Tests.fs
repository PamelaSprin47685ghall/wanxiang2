module Tests
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Server

let o (s: string) = JsonNode.Parse(s).AsObject()

[<Fact>]
let ``字段读取宽容`` () =
    let x = o """{"a":" v ","b":"  ","n":3,"f":1.5,"t":true,"l":["x"," ","x","y"],"m":{"k":"v","z":1}}"""
    Assert.Equal(Some "v", PayloadFields.str x "a")
    Assert.Equal(None, PayloadFields.str x "b")
    Assert.Equal(None, PayloadFields.str x "n")
    Assert.Equal(Some 3, PayloadFields.intOf x "n")
    Assert.Equal(Some 1.5, PayloadFields.floatOf x "f")
    Assert.True(PayloadFields.boolOf x "t" false)
    Assert.False(PayloadFields.boolOf x "a" false)
    Assert.Equal<string list>(["x"; "y"], PayloadFields.stringList x "l")
    Assert.Equal<Map<string,string>>(Map ["k", "v"], PayloadFields.stringMap x "m")
