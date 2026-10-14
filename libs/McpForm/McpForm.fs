namespace Wanxiang.UI

open System
open System.Text.Json.Nodes
open System.Text.RegularExpressions

/// MCP 编辑器的字段（泛名，限定访问）
[<RequireQualifiedAccess>]
type McpField =
    | Id
    | Label
    | Command
    | Url
    | Timeout

/// 校验通过（或未通过时也照样算出）的值
type McpValues =
    { id: string; label: string; command: string; args: string list; url: string; timeout: int }

module McpForm =

    let private trim (s: string) = if isNull s then "" else s.Trim()

    /// 参数框：按行切，去空白，丢空行
    let parseArgs (text: string) =
        (if isNull text then "" else text).Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> List.ofArray

    /// 稳定标识的校验（与模型服务编辑器同一套文案）
    let idErrors (isNew: bool) (taken: string list) (id: string) =
        if String.IsNullOrWhiteSpace id then [ "稳定标识不能为空。" ]
        elif isNew && List.contains id taken then [ "该标识已存在，请换一个。" ]
        elif not (Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$")) then [ "稳定标识仅支持英文字母、数字、下划线(_)与连字符(-)。" ]
        else []

    /// 旧 SettingsTools 编辑器 saveCore 的校验。错误按旧顺序给出；空列表即可保存
    let validate (isNew: bool) (taken: string list) (idText: string) (labelText: string) (commandText: string) (argsText: string) (urlText: string) (timeoutText: string) =
        let id, label, command, url, rawTimeout = trim idText, trim labelText, trim commandText, trim urlText, trim timeoutText
        let errors =
            [ for m in idErrors isNew taken id -> McpField.Id, m
              if String.IsNullOrWhiteSpace label then yield McpField.Label, "显示名称不能为空。"
              if String.IsNullOrWhiteSpace command && String.IsNullOrWhiteSpace url then
                  yield McpField.Command, "本地命令与远程端点至少填一个。"
                  yield McpField.Url, "本地命令与远程端点至少填一个。"
              elif not (String.IsNullOrWhiteSpace command) && not (String.IsNullOrWhiteSpace url) then
                  yield McpField.Command, "与远程端点只能填写一个。"
                  yield McpField.Url, "与本地命令只能填写一个。"
              elif not (String.IsNullOrWhiteSpace url) then
                  if not (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) then
                      yield McpField.Url, "远程端点地址必须以 http:// 或 https:// 开头。"
                  else
                      match Uri.TryCreate(url, UriKind.Absolute) with
                      | true, uri when uri.Scheme = "http" || uri.Scheme = "https" -> ()
                      | _ -> yield McpField.Url, "请输入合法的 HTTP/HTTPS 远程端点地址。"
              if String.IsNullOrWhiteSpace rawTimeout then yield McpField.Timeout, "调用超时时间不能为空。"
              else
                  match Int32.TryParse rawTimeout with
                  | true, t when t >= 1 && t <= 3600 -> ()
                  | true, _ -> yield McpField.Timeout, "请输入 1 到 3600 之间的秒数。"
                  | false, _ -> yield McpField.Timeout, "请输入有效的正整数秒数（1–3600）。" ]
        let timeout = match Int32.TryParse rawTimeout with | true, t -> t | _ -> 60
        { id = id; label = label; command = command; args = parseArgs argsText; url = url; timeout = timeout }, errors

    /// 写入载荷：空命令、空参数、空端点不写
    let payload (v: McpValues) (enabled: bool) =
        let o = JsonObject()
        o["id"] <- v.id
        o["label"] <- v.label
        if not (String.IsNullOrWhiteSpace v.command) then o["command"] <- v.command
        if not (List.isEmpty v.args) then o["args"] <- JsonArray([| for a in v.args -> JsonNode.op_Implicit a |])
        if not (String.IsNullOrWhiteSpace v.url) then o["url"] <- v.url
        o["callTimeoutSeconds"] <- v.timeout
        o["enabled"] <- enabled
        o
