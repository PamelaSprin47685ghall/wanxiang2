namespace Wanxiang.UI

open System
open System.Globalization
open System.Text.Json.Nodes

/// 生成设置表单里的字段。
[<RequireQualifiedAccess>]
type GenerationField =
    | Temperature
    | TopP
    | MaxTokens
    | ContextMessages
    | ToolRounds

/// 校验通过的数值。
type GenerationValues =
    { temperature: float option
      topP: float option
      maxTokens: int option
      maxContextMessages: int
      maxToolRounds: int }

/// 设置页「生成」分组的校验与保存载荷（旧 Views/SettingsGeneral.fs 的 SaveGenerationCore 与 floatText）。
module GenerationForm =

    /// 浮点字段的回填文本：最多两位小数，不变文化。
    let floatText (value: float option) =
        match value with
        | Some v -> v.ToString("0.##", CultureInfo.InvariantCulture)
        | None -> ""

    /// 逐字段校验（输入取 Trim 后的文本，null 视为空）。全部通过给 Ok；否则按字段次序给出每个错误与文案。
    let validate (temperature: string) (topP: string) (maxTokens: string) (contextMessages: string) (toolRounds: string)
        : Result<GenerationValues, (GenerationField * string) list> =
        let errors = ResizeArray<GenerationField * string>()
        let raw (s: string) = if isNull s then "" else s.Trim()
        let optionalFloat field (text: string) valid message =
            let text = raw text
            if String.IsNullOrWhiteSpace text then None
            else
                match Double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, value when valid value -> Some value
                | _ ->
                    errors.Add(field, message)
                    None
        let optionalPositiveInt field (text: string) =
            let text = raw text
            if String.IsNullOrWhiteSpace text then None
            else
                match Int32.TryParse text with
                | true, value when value > 0 -> Some value
                | _ ->
                    errors.Add(field, "请输入大于 0 的整数，或留空。")
                    None
        let requiredInt field (text: string) valid message =
            match Int32.TryParse(raw text) with
            | true, value when valid value -> Some value
            | _ ->
                errors.Add(field, message)
                None
        let t = optionalFloat GenerationField.Temperature temperature (fun v -> v >= 0.0 && v <= 2.0) "请输入 0–2 之间的数字，或留空。"
        let p = optionalFloat GenerationField.TopP topP (fun v -> v >= 0.0 && v <= 1.0) "请输入 0 到 1 之间的数字，或留空。"
        let m = optionalPositiveInt GenerationField.MaxTokens maxTokens
        let c = requiredInt GenerationField.ContextMessages contextMessages (fun v -> v >= 0) "请输入 0 或正整数。"
        let r = requiredInt GenerationField.ToolRounds toolRounds (fun v -> v > 0) "请输入大于 0 的整数。"
        if errors.Count = 0 then
            Ok { temperature = t; topP = p; maxTokens = m; maxContextMessages = c.Value; maxToolRounds = r.Value }
        else Error(List.ofSeq errors)

    /// 系统指令：去首尾空白，空则为 None。
    let instructions (raw: string) : string option =
        let text = if isNull raw then "" else raw.Trim()
        if String.IsNullOrWhiteSpace text then None else Some text

    /// 发给主机的更新载荷。键序与旧实现相同（线上字节不变）：未设的可选项写 null。
    let payload (values: GenerationValues) (instructions: string option) (autoTitle: bool) : JsonObject =
        let payload = JsonObject()
        match values.temperature with Some v -> payload["temperature"] <- v | None -> payload["temperature"] <- null
        match values.topP with Some v -> payload["topP"] <- v | None -> payload["topP"] <- null
        match values.maxTokens with Some v -> payload["maxTokens"] <- v | None -> payload["maxTokens"] <- null
        payload["instructions"] <- (match instructions with Some s -> JsonNode.op_Implicit s | None -> null)
        payload["maxContextMessages"] <- values.maxContextMessages
        payload["maxToolRounds"] <- values.maxToolRounds
        payload["autoTitle"] <- autoTitle
        payload
