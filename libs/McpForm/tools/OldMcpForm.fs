namespace Wanxiang.UI

// 照抄旧 Views/SettingsTools.fs 编辑器的 saveCore（TextBox 换成字段标记）与 mcpPayload
open System
open System.Text.Json.Nodes
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
type McpField =
    | Id
    | Label
    | Command
    | Url
    | Timeout

type McpValues = { id: string; label: string; command: string; args: string list; url: string; timeout: int }

module McpForm =
    let mcpPayload (id: string) (label: string) (command: string) (args: string list) (url: string) (timeout: int) (enabled: bool) =
        let o = JsonObject()
        o["id"] <- id
        o["label"] <- label
        if not (String.IsNullOrWhiteSpace command) then o["command"] <- command
        if not (List.isEmpty args) then
            o["args"] <- JsonArray([| for a in args -> JsonNode.op_Implicit a |])
        if not (String.IsNullOrWhiteSpace url) then o["url"] <- url
        o["callTimeoutSeconds"] <- timeout
        o["enabled"] <- enabled
        o

    let payload (v: McpValues) enabled = mcpPayload v.id v.label v.command v.args v.url v.timeout enabled

    let validate (isNew: bool) (takenIds: string list) (idText: string) (labelText: string) (commandText: string) (argsText: string) (urlText: string) (timeoutText: string) =
        let existingIsNone = isNew
        let id = if isNull idText then "" else idText.Trim()
        let label = if isNull labelText then "" else labelText.Trim()
        let command = if isNull commandText then "" else commandText.Trim()
        let url = if isNull urlText then "" else urlText.Trim()
        let args =
            (if isNull argsText then "" else argsText)
                .Split([| '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> List.ofArray
        let rawTimeout = if isNull timeoutText then "" else timeoutText.Trim()
        let errors = ResizeArray<McpField * string>()
        let fail box msg = errors.Add(box, msg)
        let idBox, labelBox, commandBox, urlBox, timeoutBox = McpField.Id, McpField.Label, McpField.Command, McpField.Url, McpField.Timeout

        if String.IsNullOrWhiteSpace id then
            fail idBox "稳定标识不能为空。"
        elif existingIsNone && List.contains id takenIds then
            fail idBox "该标识已存在，请换一个。"
        elif not (Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$")) then
            fail idBox "稳定标识仅支持英文字母、数字、下划线(_)与连字符(-)。"

        if String.IsNullOrWhiteSpace label then
            fail labelBox "显示名称不能为空。"

        if String.IsNullOrWhiteSpace command && String.IsNullOrWhiteSpace url then
            fail commandBox "本地命令与远程端点至少填一个。"
            fail urlBox "本地命令与远程端点至少填一个。"
        elif not (String.IsNullOrWhiteSpace command) && not (String.IsNullOrWhiteSpace url) then
            fail commandBox "与远程端点只能填写一个。"
            fail urlBox "与本地命令只能填写一个。"
        elif not (String.IsNullOrWhiteSpace url) then
            if not (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) then
                fail urlBox "远程端点地址必须以 http:// 或 https:// 开头。"
            else
                match Uri.TryCreate(url, UriKind.Absolute) with
                | true, uri when uri.Scheme = "http" || uri.Scheme = "https" -> ()
                | _ -> fail urlBox "请输入合法的 HTTP/HTTPS 远程端点地址。"

        if String.IsNullOrWhiteSpace rawTimeout then
            fail timeoutBox "调用超时时间不能为空。"
        else
            match Int32.TryParse rawTimeout with
            | true, t when t >= 1 && t <= 3600 -> ()
            | true, _ -> fail timeoutBox "请输入 1 到 3600 之间的秒数。"
            | false, _ -> fail timeoutBox "请输入有效的正整数秒数（1–3600）。"

        let timeout = match Int32.TryParse rawTimeout with true, t -> t | _ -> 60
        { id = id; label = label; command = command; args = args; url = url; timeout = timeout }, List.ofSeq errors
