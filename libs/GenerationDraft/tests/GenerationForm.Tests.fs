module GenerationFormTests
open System
open System.IO
open Xunit
open Wanxiang.UI

let expected () = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "golden", "GenerationForm", "generationform.txt"))

/// 金样由旧代码（从 旧 Views/SettingsGeneral.fs 的 SaveGenerationCore 与 floatText，照抄 摘出的函数）跑同一份 Samples 与 Render 生成
[<Fact>]
let ``golden: output matches the old code`` () =
    Assert.Equal(expected (), GenerationFormRender.render ())

// ---- 文档规则的真实行为

let private ok t p m c r =
    match GenerationForm.validate t p m c r with
    | Ok v -> v
    | Error e -> failwithf "%A" e

let private errs t p m c r =
    match GenerationForm.validate t p m c r with
    | Ok _ -> []
    | Error e -> List.map fst e

[<Fact>]
let ``blank optional fields mean unset and required fields must parse`` () =
    let v = ok "" " " null "200" "12"
    Assert.Equal(None, v.temperature)
    Assert.Equal(None, v.topP)
    Assert.Equal(None, v.maxTokens)
    Assert.Equal<GenerationField list>([ GenerationField.ContextMessages; GenerationField.ToolRounds ], errs "" "" "" "" "")

[<Fact>]
let ``ranges are inclusive at the documented bounds`` () =
    Assert.Equal(Some 2.0, (ok "2" "1" "1" "0" "1").temperature)
    Assert.Equal<GenerationField list>([ GenerationField.Temperature ], errs "2.0001" "" "" "0" "1")
    Assert.Equal<GenerationField list>([ GenerationField.TopP ], errs "" "1.01" "" "0" "1")
    Assert.Equal<GenerationField list>([ GenerationField.MaxTokens ], errs "" "" "0" "0" "1")
    Assert.Equal<GenerationField list>([ GenerationField.ContextMessages ], errs "" "" "" "-1" "1")
    Assert.Equal<GenerationField list>([ GenerationField.ToolRounds ], errs "" "" "" "0" "0")

[<Fact>]
let ``decimal separator is always a dot`` () =
    Assert.Equal<GenerationField list>([ GenerationField.Temperature ], errs "0,5" "" "" "0" "1")
    Assert.Equal(Some 0.5, (ok "0.5" "" "" "0" "1").temperature)

[<Fact>]
let ``payload writes every key with null for unset values in a fixed order`` () =
    let v = ok "" "" "" "3" "4"
    Assert.Equal(
        """{"temperature":null,"topP":null,"maxTokens":null,"instructions":null,"maxContextMessages":3,"maxToolRounds":4,"autoTitle":true}""",
        (GenerationForm.payload v None true).ToJsonString())

[<Fact>]
let ``instructions are trimmed and blank means none`` () =
    Assert.Equal(Some "a b", GenerationForm.instructions "  a b \n")
    Assert.Equal(None, GenerationForm.instructions " \n ")
    Assert.Equal(None, GenerationForm.instructions null)
