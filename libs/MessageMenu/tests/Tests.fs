module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "messagemenu.txt"))

/// 金样由旧代码（旧 Views/MessageCard.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

[<Fact>]
let ``streaming hides the whole menu`` () =
    Assert.Empty(MessageMenu.entries true false true "x" (Some 1UL) true)

[<Fact>]
let ``user gets edit, last assistant gets regenerate with hints`` () =
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Copy; MessageMenuItem.EditAndFork; MessageMenuItem.CopyLink 7UL; MessageMenuItem.Delete 7UL ], MessageMenu.entries false true false "hi" (Some 7UL) true)
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Copy; MessageMenuItem.Regenerate ], MessageMenu.entries false false true "hi" None true)
    Assert.Equal("Ctrl+Shift+C", MessageMenu.hint false true MessageMenuItem.Copy)
    Assert.Equal("", MessageMenu.hint true false MessageMenuItem.Copy)

[<Fact>]
let ``blank text has no copy; link needs a sink; delete is danger`` () =
    Assert.Equal<MessageMenuItem list>([ MessageMenuItem.Delete 3UL ], MessageMenu.entries false false false " " (Some 3UL) false)
    Assert.True(MessageMenu.isDanger (MessageMenuItem.Delete 3UL))
