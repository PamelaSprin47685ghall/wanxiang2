namespace Wanxiang.UI

open System
open System.Globalization
open System.Text.Json.Nodes

/// 旧 SettingsGeneral.SaveGenerationCore 的校验与载荷照抄（文本框换成字符串，错误记字段名）
module GenerationForm =
    let floatText (value: float option) =
        match value with
        | Some v -> v.ToString("0.##", CultureInfo.InvariantCulture)
        | None -> ""

    /// 旧流程：校验，全部通过则组载荷；返回 (错误表, 载荷 JSON)
    let run (temperatureBox: string) (topPBox: string) (maxTokensBox: string) (contextBox: string) (toolRoundsBox: string) (instructionsText: string) (autoTitle: bool) =
        let errors = ResizeArray<string * string>()
        let fail (box: string * string) message = errors.Add(fst box, message)
        let raw (box: string * string) = let t = snd box in if isNull t then "" else t.Trim()
        let parseOptionalFloat box valid message =
            let text = raw box
            if String.IsNullOrWhiteSpace text then None
            else
                match Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, value when valid value -> Some value
                | _ ->
                    fail box message
                    None
        let parseOptionalPositiveInt box =
            let text = raw box
            if String.IsNullOrWhiteSpace text then None
            else
                match Int32.TryParse text with
                | true, value when value > 0 -> Some value
                | _ ->
                    fail box "请输入大于 0 的整数，或留空。"
                    None
        let parseRequiredInt box valid message =
            match Int32.TryParse(raw box) with
            | true, value when valid value -> Some value
            | _ ->
                fail box message
                None
        let temperature = parseOptionalFloat ("Temperature", temperatureBox) (fun value -> value >= 0.0 && value <= 2.0) "请输入 0–2 之间的数字，或留空。"
        let topP = parseOptionalFloat ("TopP", topPBox) (fun value -> value >= 0.0 && value <= 1.0) "请输入 0 到 1 之间的数字，或留空。"
        let maxTokens = parseOptionalPositiveInt ("MaxTokens", maxTokensBox)
        let contextMessages = parseRequiredInt ("ContextMessages", contextBox) (fun value -> value >= 0) "请输入 0 或正整数。"
        let toolRounds = parseRequiredInt ("ToolRounds", toolRoundsBox) (fun value -> value > 0) "请输入大于 0 的整数。"
        if List.isEmpty (List.ofSeq errors) then
            let payload = JsonObject()
            match temperature with Some v -> payload["temperature"] <- v | None -> payload["temperature"] <- null
            match topP with Some v -> payload["topP"] <- v | None -> payload["topP"] <- null
            match maxTokens with Some v -> payload["maxTokens"] <- v | None -> payload["maxTokens"] <- null
            let instructions = if isNull instructionsText then "" else instructionsText.Trim()
            payload["instructions"] <- if String.IsNullOrWhiteSpace instructions then null else JsonNode.op_Implicit instructions
            payload["maxContextMessages"] <- contextMessages.Value
            payload["maxToolRounds"] <- toolRounds.Value
            payload["autoTitle"] <- autoTitle
            [], payload.ToJsonString()
        else List.ofSeq errors, ""


module Adapter =
    let run t p m c r i a = GenerationForm.run t p m c r i a
