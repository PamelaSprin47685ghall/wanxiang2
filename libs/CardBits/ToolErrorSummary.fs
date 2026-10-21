namespace Wanxiang.UI

open System
open System.Text.Json

module ToolErrorSummary =

    /// 从工具调用结果中尝试提取错误摘要。
    /// 若返回 Some msg 说明调用失败并携带错误说明；若为 None 说明没有检测到错误。
    let tryExtract (resultOpt: string option) : string option =
        match resultOpt with
        | Some res when not (String.IsNullOrWhiteSpace res) ->
            let trimmed = res.Trim()

            if trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase) then
                let msg = trimmed.Substring(6).Trim()
                Some(if String.IsNullOrWhiteSpace msg then "执行失败" else msg)
            elif trimmed.StartsWith "{" && trimmed.EndsWith "}" then
                try
                    use doc = JsonDocument.Parse trimmed
                    let root = doc.RootElement

                    if root.ValueKind = JsonValueKind.Object then
                        let mutable errProp = Unchecked.defaultof<_>
                        let mutable isErrProp = Unchecked.defaultof<_>
                        let mutable successProp = Unchecked.defaultof<_>
                        let mutable statusProp = Unchecked.defaultof<_>

                        let tryExtractMessageFromObject (obj: JsonElement) : string option =
                            let mutable p = Unchecked.defaultof<_>

                            if
                                obj.TryGetProperty("message", &p)
                                && p.ValueKind = JsonValueKind.String
                                && not (String.IsNullOrWhiteSpace(p.GetString()))
                            then
                                Some(p.GetString().Trim())
                            elif
                                obj.TryGetProperty("msg", &p)
                                && p.ValueKind = JsonValueKind.String
                                && not (String.IsNullOrWhiteSpace(p.GetString()))
                            then
                                Some(p.GetString().Trim())
                            elif
                                obj.TryGetProperty("reason", &p)
                                && p.ValueKind = JsonValueKind.String
                                && not (String.IsNullOrWhiteSpace(p.GetString()))
                            then
                                Some(p.GetString().Trim())
                            elif obj.TryGetProperty("error", &p) then
                                match p.ValueKind with
                                | JsonValueKind.String when not (String.IsNullOrWhiteSpace(p.GetString())) ->
                                    Some(p.GetString().Trim())
                                | JsonValueKind.Object ->
                                    let mutable innerMsg = Unchecked.defaultof<_>

                                    if
                                        p.TryGetProperty("message", &innerMsg)
                                        && innerMsg.ValueKind = JsonValueKind.String
                                        && not (String.IsNullOrWhiteSpace(innerMsg.GetString()))
                                    then
                                        Some(innerMsg.GetString().Trim())
                                    else
                                        Some(p.GetRawText())
                                | _ -> None
                            else
                                None

                        if
                            root.TryGetProperty("error", &errProp)
                            && errProp.ValueKind <> JsonValueKind.Null
                            && errProp.ValueKind <> JsonValueKind.False
                        then
                            match errProp.ValueKind with
                            | JsonValueKind.String -> Some(errProp.GetString())
                            | JsonValueKind.Object ->
                                let mutable msgProp = Unchecked.defaultof<_>

                                if
                                    errProp.TryGetProperty("message", &msgProp)
                                    && msgProp.ValueKind = JsonValueKind.String
                                then
                                    Some(msgProp.GetString())
                                else
                                    Some(errProp.GetRawText())
                            | JsonValueKind.True -> Some("执行失败")
                            | _ -> Some(errProp.GetRawText())
                        elif
                            (root.TryGetProperty("isError", &isErrProp)
                             && isErrProp.ValueKind = JsonValueKind.True)
                            || (root.TryGetProperty("is_error", &isErrProp)
                                && isErrProp.ValueKind = JsonValueKind.True)
                        then
                            Some(defaultArg (tryExtractMessageFromObject root) "执行失败")
                        elif
                            root.TryGetProperty("success", &successProp)
                            && successProp.ValueKind = JsonValueKind.False
                        then
                            Some(defaultArg (tryExtractMessageFromObject root) "执行失败")
                        elif
                            root.TryGetProperty("status", &statusProp)
                            && statusProp.ValueKind = JsonValueKind.String
                        then
                            let statusVal = statusProp.GetString().Trim()

                            if
                                statusVal.Equals("error", StringComparison.OrdinalIgnoreCase)
                                || statusVal.Equals("failed", StringComparison.OrdinalIgnoreCase)
                                || statusVal.Equals("failure", StringComparison.OrdinalIgnoreCase)
                            then
                                Some(defaultArg (tryExtractMessageFromObject root) "执行失败")
                            else
                                None
                        else
                            None
                    else
                        None
                with _ ->
                    None
            elif trimmed.IndexOf("traceback", StringComparison.OrdinalIgnoreCase) >= 0 then
                // Python traceback：摘要取最后一行非空行（真正的异常行），全文仍在详情里。
                let lastLine =
                    trimmed.Split('\n')
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (not << String.IsNullOrWhiteSpace)
                    |> Array.tryLast

                Some(defaultArg lastLine "执行失败")
            elif trimmed.IndexOf("command failed", StringComparison.OrdinalIgnoreCase) >= 0 then
                // shell 失败：摘要取首行非空行，全文仍在详情里。
                let firstLine =
                    trimmed.Split('\n')
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (not << String.IsNullOrWhiteSpace)
                    |> Array.tryHead

                Some(defaultArg firstLine "执行失败")
            elif
                trimmed.IndexOf("500 internal server error", StringComparison.OrdinalIgnoreCase)
                >= 0
            then
                Some("500 Internal Server Error")
            elif
                trimmed.IndexOf("internal server error", StringComparison.OrdinalIgnoreCase)
                >= 0
            then
                Some("Internal Server Error")
            else
                let lines =
                    trimmed.Split([| '\r'; '\n' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Array.map (fun s -> s.Trim())
                    |> Array.filter (not << String.IsNullOrWhiteSpace)

                let tryMatchFatal () =
                    lines
                    |> Array.tryFind (fun l -> l.StartsWith("fatal:", StringComparison.OrdinalIgnoreCase))
                    |> Option.map (fun l ->
                        let rest = l.Substring(6).Trim()
                        if String.IsNullOrWhiteSpace rest then "执行失败" else rest)

                let tryMatchHttpStatus () =
                    let isStatusLine (l: string) =
                        let stripped =
                            if l.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) then
                                let idx = l.IndexOf(' ')
                                if idx > 0 then l.Substring(idx).Trim() else l
                            elif l.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase) then
                                l.Substring(5).Trim()
                            else
                                l

                        if
                            stripped.Equals("401 Unauthorized", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("401: Unauthorized", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("401 Unauthorized", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("401: Unauthorized", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "401 Unauthorized"
                        elif
                            stripped.Equals("403 Forbidden", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("403: Forbidden", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("403 Forbidden", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("403: Forbidden", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "403 Forbidden"
                        elif
                            stripped.Equals("404 Not Found", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("404: Not Found", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("404 Not Found", StringComparison.OrdinalIgnoreCase)
                            || stripped.StartsWith("404: Not Found", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "404 Not Found"
                        else
                            None

                    let isExactStatus (l: string) =
                        let stripped =
                            if l.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase) then
                                let idx = l.IndexOf(' ')
                                if idx > 0 then l.Substring(idx).Trim() else l
                            elif l.StartsWith("HTTP ", StringComparison.OrdinalIgnoreCase) then
                                l.Substring(5).Trim()
                            else
                                l

                        if
                            stripped.Equals("401 Unauthorized", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("401: Unauthorized", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "401 Unauthorized"
                        elif
                            stripped.Equals("403 Forbidden", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("403: Forbidden", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "403 Forbidden"
                        elif
                            stripped.Equals("404 Not Found", StringComparison.OrdinalIgnoreCase)
                            || stripped.Equals("404: Not Found", StringComparison.OrdinalIgnoreCase)
                        then
                            Some "404 Not Found"
                        else
                            None

                    match lines |> Array.tryHead with
                    | Some firstLine when (isStatusLine firstLine).IsSome -> isStatusLine firstLine
                    | _ -> lines |> Array.tryPick isExactStatus

                let tryMatchPermissionDenied () =
                    lines
                    |> Array.tryFind (fun l ->
                        let s = l.Trim()

                        s.Equals("permission denied", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("permission denied.", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("access denied", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("access denied.", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("permission denied:", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("access denied:", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("access denied for ", StringComparison.OrdinalIgnoreCase)
                        || s.IndexOf(": permission denied", StringComparison.OrdinalIgnoreCase) >= 0
                        || s.IndexOf(": access denied", StringComparison.OrdinalIgnoreCase) >= 0)

                let tryMatchCommandNotFound () =
                    lines
                    |> Array.tryFind (fun l ->
                        let s = l.Trim()

                        s.Equals("command not found", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("command not found.", StringComparison.OrdinalIgnoreCase)
                        || s.StartsWith("command not found:", StringComparison.OrdinalIgnoreCase)
                        || s.EndsWith(": command not found", StringComparison.OrdinalIgnoreCase)
                        || s.EndsWith(": command not found.", StringComparison.OrdinalIgnoreCase)
                        || s.IndexOf(": command not found", StringComparison.OrdinalIgnoreCase) >= 0)

                match tryMatchFatal () with
                | Some err -> Some err
                | None ->
                    match tryMatchHttpStatus () with
                    | Some err -> Some err
                    | None ->
                        match tryMatchPermissionDenied () with
                        | Some err -> Some err
                        | None ->
                            match tryMatchCommandNotFound () with
                            | Some err -> Some err
                            | None -> None
        | _ -> None
