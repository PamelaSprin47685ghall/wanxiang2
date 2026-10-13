module AguiMessageMapTests

open Microsoft.Extensions.AI
open Xunit
open Wanxiang.Agent

[<Fact>]
let ``a user message round trips through AG-UI with its content`` () =
    let node = MessageMap.toAgui (ChatMessage(ChatRole.User, "你好"))
    Assert.Equal("user", node["role"].GetValue<string>())
    Assert.Equal("你好", node["content"].GetValue<string>())
    let back = (MessageMap.tryToMaf node).Value
    Assert.Equal(ChatRole.User, back.Role)
    Assert.Equal("你好", back.Text)
