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

// SPEC 4：超出 double 精确范围的整数会被静默改写（RFC 8785 附录 D），约束本身锁进测试
[<Fact>]
let ``numbers are serialized as doubles: big integers are rewritten, equal numbers are equal`` () =
    // 2^53-1 仍是精确值
    Assert.Equal("""{"n":9007199254740991}""", Jcs.text """{"n":9007199254740991}""")
    // 2^53+1 落回最近的 double：值变了，哈希仍稳定（所以要靠 JSON 字符串承载大整数）
    Assert.Equal("""{"n":9007199254740992}""", Jcs.text """{"n":9007199254740993}""")
    Assert.Equal(Jcs.sha256Hex """{"n":9007199254740993}""", Jcs.sha256Hex """{"n":9007199254740992}""")
    // 更大的整数走指数写法
    Assert.Equal("""{"n":1.2345678901234568e+29}""", Jcs.text """{"n":123456789012345678901234567890}""")
    // 等值写法归一：1.0 = 1、1e2 = 100、-0 = 0
    Assert.Equal("""{"n":1}""", Jcs.text """{"n":1.0}""")
    Assert.Equal("""{"n":100}""", Jcs.text """{"n":1e2}""")
    Assert.Equal("""{"n":0}""", Jcs.text """{"n":-0}""")
