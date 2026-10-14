module Tests

open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

/// 金样由旧代码（从旧视图文件原样摘出的函数）在同一组输入上生成
let render () =
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let js (s: string) = Text.Json.JsonSerializer.Serialize s
    for a in Samples.args do line (sprintf "displayArguments %s -> %s" (js a) (js (ApprovalCardText.displayArguments a)))
    for e in [ true; false ] do line (sprintf "argsToggleText %b -> %s" e (ApprovalCardText.argsToggleText e))
    for p in Samples.policies do line (sprintf "policyNote %s -> %s" (js p) (ApprovalCardText.policyNote p))
    for e in [ true; false ] do line (sprintf "sentText %b -> %s" e (ApprovalCardText.sentText e))
    for s in Samples.statuses do line (sprintf "alreadyHandledText %s -> %s" (js s) (ApprovalCardText.alreadyHandledText s))
    sb.ToString()

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "approval.txt"))

[<Fact>]
let ``golden: approval card texts match the old ToolApprovalCard`` () =
    Assert.Equal(expected (), render ())


[<Fact>]
let ``escaped chinese in arguments is shown as is`` () =
    Assert.Equal("{\"p\":\"中文\"}", ApprovalCardText.displayArguments "{\"p\":\"\\u4e2d\\u6587\"}")

[<Fact>]
let ``unparsable or blank arguments come back unchanged`` () =
    Assert.Equal("not json", ApprovalCardText.displayArguments "not json")
    Assert.Equal("  ", ApprovalCardText.displayArguments "  ")
    Assert.Null(ApprovalCardText.displayArguments null)

[<Fact>]
let ``unknown policy and status fall back to the generic text`` () =
    Assert.Equal("每次调用这个工具都会询问。", ApprovalCardText.policyNote "whatever")
    Assert.Equal("这个审批已经处理过了。", ApprovalCardText.alreadyHandledText "")
