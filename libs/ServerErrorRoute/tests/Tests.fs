module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/ 下原样摘出的旧代码跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

// 行为：记账失败没有当前对话时只能 toast。
[<Fact>]
let ``D9 消息记账失败进当前对话的错误卡，没有当前对话时 toast`` () =
    Assert.Equal(ServerErrorRoute.ConversationFailure "消息记账失败：x", ServerErrorRoute.classify true "消息记账失败：x")
    Assert.Equal(ServerErrorRoute.Toast "主机报告了错误：消息记账失败：x", ServerErrorRoute.classify false "消息记账失败：x")

[<Fact>]
let ``缺失附件按小写 sha256 标记`` () =
    Assert.Equal(ServerErrorRoute.MissingAttachment "abcdef", ServerErrorRoute.classify false "attachment ABCDEF not found")
