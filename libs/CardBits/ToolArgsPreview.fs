namespace Wanxiang.UI

open System
open System.Text.Json

module ToolArgsPreview =

    /// 对象或数组缩进排版；不是 JSON 或解析失败就原样返回。
    let prettyJson (raw: string) : string =
        if String.IsNullOrWhiteSpace raw then ""
        else
            let trimmed = raw.Trim()
            if (trimmed.StartsWith "{" && trimmed.EndsWith "}") || (trimmed.StartsWith "[" && trimmed.EndsWith "]") then
                try
                    use doc = JsonDocument.Parse trimmed
                    let options = JsonSerializerOptions(WriteIndented = true)
                    JsonSerializer.Serialize(doc.RootElement, options)
                with _ -> raw
            else raw

    /// 参数摘要：顶层键数与一行预览（字符串截到 30 字、整行截到 95 字）。
    let preview (argsJson: string) : int * string =
        if String.IsNullOrWhiteSpace argsJson then 0, ""
        else
            let trimmed = argsJson.Trim()
            try
                use doc = JsonDocument.Parse trimmed
                match doc.RootElement.ValueKind with
                | JsonValueKind.Object ->
                    let props = [ for p in doc.RootElement.EnumerateObject() -> p.Name, p.Value ]
                    let count = props.Length
                    if count = 0 then 0, "无参数"
                    else
                        let formatVal (v: JsonElement) =
                            match v.ValueKind with
                            | JsonValueKind.String ->
                                let s = v.GetString().Replace('\n', ' ').Replace('\r', ' ').Trim()
                                if s.Length <= 32 then sprintf "\"%s\"" s else sprintf "\"%s…\"" (s.Substring(0, 30))
                            | JsonValueKind.Number -> v.GetRawText()
                            | JsonValueKind.True -> "true"
                            | JsonValueKind.False -> "false"
                            | JsonValueKind.Null -> "null"
                            | JsonValueKind.Array -> sprintf "[%d]" (v.GetArrayLength())
                            | JsonValueKind.Object -> sprintf "{%d}" (v.EnumerateObject() |> Seq.length)
                            | _ -> v.GetRawText()
                        let pairs = props |> List.map (fun (name, v) -> sprintf "%s: %s" name (formatVal v))
                        let joined = String.Join(", ", pairs)
                        count, (if joined.Length > 96 then joined.Substring(0, 95) + "…" else joined)
                | JsonValueKind.Array ->
                    let count = doc.RootElement.GetArrayLength()
                    count, sprintf "[%d 项]" count
                | _ ->
                    let s = trimmed.Replace('\n', ' ').Replace('\r', ' ')
                    1, (if s.Length > 96 then s.Substring(0, 95) + "…" else s)
            with _ ->
                let single = trimmed.Replace('\n', ' ').Replace('\r', ' ')
                1, (if single.Length > 96 then single.Substring(0, 95) + "…" else single)
