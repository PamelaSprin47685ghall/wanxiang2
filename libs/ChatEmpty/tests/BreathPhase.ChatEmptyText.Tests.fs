module BreathPhaseChatEmptyTextTests
open System
open System.IO
open Xunit
open Wanxiang.UI


[<Fact>]
let ``brand shows only before a conversation can start`` () =
    Assert.True(ChatEmptyState.showsBrand NotConnected)
    Assert.True(ChatEmptyState.showsBrand (ConnectionFailed "x"))
    Assert.True(ChatEmptyState.showsBrand NoProvider)
    Assert.False(ChatEmptyState.showsBrand NoConversation)
    Assert.False(ChatEmptyState.showsBrand EmptyConversation)

[<Fact>]
let ``connection failure hint carries the reason`` () =
    Assert.StartsWith("连接被拒绝。", snd (ChatEmptyText.text (ConnectionFailed "连接被拒绝")))
