module ContinuableReplyTests
open System.Text.Json.Nodes
open Xunit
open Wanxiang.Server

let private j (s: string) = JsonNode.Parse s

[<Fact>]
let ``助手且有正文才算可续写`` () =
    Assert.True(ContinuableReply.hasText (j """{"role":"assistant","contents":[{"text":"有字"}]}"""))
    Assert.False(ContinuableReply.hasText (j """{"role":"assistant","contents":[{"text":"   "}]}"""))
    Assert.False(ContinuableReply.hasText (j """{"role":"user","contents":[{"text":"x"}]}"""))

[<Fact>]
let ``任一段有正文即可，其他段可以是工具调用`` () =
    Assert.True(ContinuableReply.hasText (j """{"role":"assistant","contents":[{"type":"functionCall"},{"text":""},{"text":"a"}]}"""))

[<Fact>]
let ``形状不对一律为否且不抛`` () =
    for s in [ "1"; "[]"; """{"role":1,"contents":[{"text":"a"}]}"""; """{"role":"assistant"}"""
               """{"role":"assistant","contents":{"text":"a"}}"""; """{"role":"assistant","contents":[{"text":5}]}"""
               """{"role":"assistant","contents":["a"]}""" ] do
        Assert.False(ContinuableReply.hasText (j s), s)
    Assert.False(ContinuableReply.hasText null)

[<Fact>]
let ``角色大小写敏感`` () =
    Assert.False(ContinuableReply.hasText (j """{"role":"Assistant","contents":[{"text":"a"}]}"""))
