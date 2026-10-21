module ApprovalCardTextTests

open System
open System.IO
open System.Text
open Xunit
open Wanxiang.UI

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

[<Fact>]
let ``other conversation waiting text is the old toast`` () =
    Assert.Equal("另一个对话里有工具调用在等你批准。", ApprovalCardText.otherConversationWaitingText)
