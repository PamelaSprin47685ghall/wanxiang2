module Tests

open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "mcpform.txt"))

/// 金样由旧代码（旧 Views/SettingsTools.fs，照抄分支）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), Render.render ())

// ---- 文档规则的真实行为

let run c u t = snd (McpForm.validate true [ "fs" ] "srv" "名" c "" u t)

[<Fact>]
let ``exactly one of command and url, both fields flagged`` () =
    Assert.Equal<(McpField * string) list>([ McpField.Command, "本地命令与远程端点至少填一个。"; McpField.Url, "本地命令与远程端点至少填一个。" ], run "" "" "60")
    Assert.Equal(2, (run "npx" "https://x" "60").Length)
    Assert.Empty(run "npx" "" "60")
    Assert.Empty(run "" "https://x/sse" "60")

[<Fact>]
let ``timeout must be 1 to 3600`` () =
    Assert.Empty(run "npx" "" "1")
    Assert.Empty(run "npx" "" "3600")
    Assert.Equal<(McpField * string) list>([ McpField.Timeout, "请输入 1 到 3600 之间的秒数。" ], run "npx" "" "0")
    Assert.Equal<(McpField * string) list>([ McpField.Timeout, "请输入有效的正整数秒数（1–3600）。" ], run "npx" "" "x")

[<Fact>]
let ``a taken id only blocks new entries`` () =
    Assert.NotEmpty(snd (McpForm.validate true [ "fs" ] "fs" "名" "npx" "" "" "60"))
    Assert.Empty(snd (McpForm.validate false [ "fs" ] "fs" "名" "npx" "" "" "60"))

[<Fact>]
let ``payload omits empty command, args and url`` () =
    let v, _ = McpForm.validate true [] "srv" "名" "" " a \n\nb" "https://x" "5"
    Assert.Equal("""{"id":"srv","label":"\u540D","args":["a","b"],"url":"https://x","callTimeoutSeconds":5,"enabled":true}""", (McpForm.payload v true).ToJsonString())
