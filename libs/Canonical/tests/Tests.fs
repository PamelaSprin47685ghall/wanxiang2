module CanonicalTests

open Xunit
open Canonical

// SPEC 1：键序与空白无关
[<Fact>]
let ``key order and whitespace do not change canonical form or hash`` () =
    let a = """{"b":2, "a":[1, 2.0, "x"]}"""
    let b = """{ "a" : [1,2,"x"],"b":2 }"""
    Assert.Equal(Jcs.text a, Jcs.text b)
    Assert.Equal(Jcs.sha256Hex a, Jcs.sha256Hex b)

// SPEC 2：RFC 8785 §3.2.3 样例（键按 UTF-16 码元排序）
[<Fact>]
let ``RFC 8785 sorting example`` () =
    let input = """{"\u20ac":"Euro Sign","\r":"Carriage Return","\ufb33":"Hebrew Letter Dalet With Dagesh","1":"One","\ud83d\ude00":"Emoji: Grinning Face","\u0080":"Control","\u00f6":"Latin Small Letter O With Diaeresis"}"""
    let keys = System.Text.Json.Nodes.JsonNode.Parse(Jcs.text input).AsObject() |> Seq.map _.Value.GetValue<string>() |> List.ofSeq
    Assert.Equal<string list>(
        [ "Carriage Return"; "One"; "Control"; "Latin Small Letter O With Diaeresis"; "Euro Sign"; "Emoji: Grinning Face"; "Hebrew Letter Dalet With Dagesh" ], keys)

// SPEC 3：哈希格式
[<Fact>]
let ``hash is 64 lowercase hex chars`` () =
    let h = Jcs.sha256Hex "{}"
    Assert.Matches("^[0-9a-f]{64}$", h)
    Assert.Equal("44136fa355b3678a1146ad16f7e8649e94fb4fc21fe77e8310c060f61caaff8a", h)
