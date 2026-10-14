module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

// 金标准：golden/expected.txt 由 tools/ 下原样摘出的旧代码跑同一份 Render.fs 生成。
[<Fact>]
let ``golden matches old code`` () =
    Assert.Equal(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "expected.txt")), Render.all ())

[<Fact>]
let ``换主机时先清旧主机的状态；同一台重连不清，导出框跨重连保留`` () =
    let switch = InstanceSwitch.plan "a" "b" false false false true
    Assert.Contains(InstanceStep.DisconnectOutbox "a", switch)
    Assert.Contains(InstanceStep.ClearConversationList, switch)
    let same = InstanceSwitch.plan "a" "a" true false false true
    Assert.DoesNotContain(InstanceStep.ClearConversationList, same)
    Assert.DoesNotContain(InstanceStep.CloseDialogs, same)
    Assert.DoesNotContain(InstanceStep.CloseExportDialog, InstanceSwitch.plan "" "a" true false false false)
