module ModelVisionTests

open Xunit
open Wanxiang.UI

// 迁自旧 AttachmentVisionTests。
[<Theory>]
[<InlineData("gpt-4o-mini", "yes")>]
[<InlineData("gpt-4.1", "yes")>]
[<InlineData("claude-sonnet-4-5", "yes")>]
[<InlineData("claude-3-5-haiku-latest", "yes")>]
[<InlineData("gemini-2.5-flash", "yes")>]
[<InlineData("llama3.2-vision:11b", "yes")>]
[<InlineData("qwen2.5-vl:7b", "yes")>]
[<InlineData("llava:13b", "yes")>]
[<InlineData("gemma3:4b", "yes")>]
[<InlineData("gpt-3.5-turbo", "no")>]
[<InlineData("o3-mini", "no")>]
[<InlineData("deepseek-chat", "no")>]
[<InlineData("llama3.1:8b", "no")>]
[<InlineData("qwen2.5:7b", "no")>]
[<InlineData("my-finetune", "unknown")>]
[<InlineData("", "unknown")>]
let ``model vision classification by name`` (model: string, expected: string) =
    let actual =
        match ModelVision.classify model with
        | VisionYes -> "yes"
        | VisionNo -> "no"
        | VisionUnknown -> "unknown"
    Assert.Equal(expected, actual)

[<Fact>]
let ``blocked message names the model and both ways out`` () =
    Assert.Equal("当前模型「X」不能识别图片，图片不会发出去。请换一个能看图的模型，或移除图片后再发送。", ModelVision.blockedMessage "X")
