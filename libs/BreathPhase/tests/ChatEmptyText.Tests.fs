module ChatEmptyTextTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "ChatEmptyText", "chatemptytext.txt"))

/// 金样由旧代码（从 旧 Views/ChatView.fs 的 ChatStarters 与 ShowEmpty 文案表 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), ChatEmptyTextRender.render ())

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
