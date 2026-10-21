module Tests

open Xunit
open Wanxiang.UI

let private opt pid label model =
    {
        providerId = pid
        providerLabel = label
        model = model
        isCurrent = false
        isDefault = false
    }

let private items =
    [
        opt "oa" "OpenAI" "gpt-4o"
        opt "oa" "OpenAI" "gpt-4o-mini"
        opt "oa" "OpenAI" "o3"
        opt "ds" "DeepSeek" "deepseek-chat"
        opt "ds" "DeepSeek" "deepseek-reasoner"
    ]

// 与旧 ModelChipRateLimitTests `switcher search matches…` 同一组断言；从目录列出选项的部分在组合者 Catalog。
[<Fact>]
let ``filter matches every term in model or provider and ranks model prefix first`` () =
    Assert.Equal<string list>(
        [ "deepseek-chat"; "deepseek-reasoner" ],
        ModelSwitcher.filter "deepseek" items |> List.map _.model
    )

    Assert.Equal<string list>([ "gpt-4o-mini" ], ModelSwitcher.filter "openai MINI" items |> List.map _.model)
    Assert.Equal("o3", (ModelSwitcher.filter "o" items |> List.head).model)
    Assert.Empty(ModelSwitcher.filter "claude" items)
    Assert.Equal(items.Length, (ModelSwitcher.filter "  " items).Length)
    Assert.Equal<string list>([ "deepseek-chat" ], ModelSwitcher.filter "ds　chat" items |> List.map _.model)

// 迁自旧 ModelChipRateLimitTests。
[<Fact>]
let ``switching copy says it applies from the next reply`` () =
    Assert.Contains("从下一条回复开始生效", ModelSwitcher.switchedToast "OpenAI" "o3")
    Assert.Contains("已有回复不会改变", ModelSwitcher.appliesNote)
