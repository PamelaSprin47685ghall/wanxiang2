namespace Wanxiang.UI

// 照抄旧 Views/SettingsProviders.fs 的 isLoopbackUrl 与编辑器 saveCore 校验段（第 423–516 行）；
// TextBox 换成字段标记，defaultModelBox 回填与 toast 换成 autoPicked，url 归一由调用方先做
open System
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
type ProviderField =
    | Id
    | Label
    | Url
    | Models
    | DefaultModel
    | Timeout
    | Retries
    | VisionYes
    | VisionNo

type ProviderFormInput =
    { isNew: bool; takenIds: string list; existing: (int * int) option; id: string; label: string; url: string
      models: string; defaultModel: string; timeout: string; retries: string; visionYes: string; visionNo: string }

type ProviderFormResult =
    { id: string; label: string; url: string; models: string list; defaultModel: string; timeout: int; retries: int
      visionYes: string list; visionNo: string list; autoPicked: string option; errors: (ProviderField * string) list }

module ProviderForm =
    let isLoopbackUrl (baseUrl: string) =
        if String.IsNullOrWhiteSpace baseUrl then false
        else
            match Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute) with
            | true, uri when uri.IsLoopback -> true
            | _ ->
                let lower = baseUrl.Trim().ToLowerInvariant()
                lower.Contains("127.0.0.1") || lower.Contains("localhost") || lower.Contains("[::1]") || lower.Contains("::1")

    let validate (inp: ProviderFormInput) : ProviderFormResult =
        let idBox, labelBox, urlBox, modelsBox, defaultModelBox, timeoutBox, retriesBox, visionYesBox, visionNoBox =
            ProviderField.Id, ProviderField.Label, ProviderField.Url, ProviderField.Models, ProviderField.DefaultModel,
            ProviderField.Timeout, ProviderField.Retries, ProviderField.VisionYes, ProviderField.VisionNo
        let text f =
            match f with
            | ProviderField.Id -> inp.id | ProviderField.Label -> inp.label | ProviderField.Url -> inp.url
            | ProviderField.Models -> inp.models | ProviderField.DefaultModel -> inp.defaultModel
            | ProviderField.Timeout -> inp.timeout | ProviderField.Retries -> inp.retries
            | ProviderField.VisionYes -> inp.visionYes | ProviderField.VisionNo -> inp.visionNo
        let existing = inp.existing |> Option.map (fun (t, r) -> {| timeoutSeconds = t; maxRetries = r |})
        let takenIds = inp.takenIds
        let existingIsNone = inp.isNew
        let mutable autoPicked = None
        let id = if isNull (text idBox) then "" else (text idBox).Trim()
        let label = if isNull (text labelBox) then "" else (text labelBox).Trim()
        let url = inp.url
        let models =
            (if isNull (text modelsBox) then "" else text modelsBox)
                .Split([| '\n'; '\r'; ',' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.distinct
            |> List.ofArray
        let errors = ResizeArray<ProviderField * string>()
        let fail box msg = errors.Add(box, msg)

        if String.IsNullOrWhiteSpace id then
            fail idBox "稳定标识不能为空。"
        elif existingIsNone && List.contains id takenIds then
            fail idBox "该标识已存在，请换一个。"
        elif not (Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$")) then
            fail idBox "稳定标识仅支持英文字母、数字、下划线(_)与连字符(-)。"

        if String.IsNullOrWhiteSpace label then
            fail labelBox "显示名称不能为空。"

        if String.IsNullOrWhiteSpace url then
            fail urlBox "端点地址不能为空。"
        elif not (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) then
            fail urlBox "端点地址必须以 http:// 或 https:// 开头。"
        else
            match Uri.TryCreate(url, UriKind.Absolute) with
            | true, uri when uri.Scheme = "http" || uri.Scheme = "https" -> ()
            | _ -> fail urlBox "请输入合法的 HTTP/HTTPS 地址（例如 https://api.openai.com/v1）。"

        if List.isEmpty models then
            fail modelsBox "至少填写一个模型名（每行一个）。"

        let rawDefaultModel = if isNull (text defaultModelBox) then "" else (text defaultModelBox).Trim()
        let defaultModel =
            if String.IsNullOrWhiteSpace rawDefaultModel then
                match models with
                | head :: _ ->
                    autoPicked <- Some head
                    head
                | [] -> ""
            elif not (List.isEmpty models) && not (List.contains rawDefaultModel models) then
                fail defaultModelBox (sprintf "默认模型“%s”不在模型列表中，请从列表中选择或留空自动选用。" rawDefaultModel)
                rawDefaultModel
            else
                rawDefaultModel

        let timeout =
            let rawTimeout = if isNull (text timeoutBox) then "" else (text timeoutBox).Trim()
            if String.IsNullOrWhiteSpace rawTimeout then
                existing |> Option.map (fun p -> p.timeoutSeconds) |> Option.defaultValue 120
            else
                match Int32.TryParse rawTimeout with
                | true, v when v > 0 -> v
                | _ ->
                    fail timeoutBox "超时时间必须是大于 0 的整秒数。"
                    120

        let retries =
            let rawRetries = if isNull (text retriesBox) then "" else (text retriesBox).Trim()
            if String.IsNullOrWhiteSpace rawRetries then
                existing |> Option.map (fun p -> p.maxRetries) |> Option.defaultValue 2
            else
                match Int32.TryParse rawRetries with
                | true, v when v >= 0 && v <= 10 -> v
                | _ ->
                    fail retriesBox "重试次数必须是 0 到 10 之间的整数。"
                    2

        let lines box =
            (if isNull (text box) then "" else text box)
                .Split([| '\n'; '\r'; ',' |], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun s -> s.Trim())
            |> Array.filter (String.IsNullOrWhiteSpace >> not)
            |> Array.distinct
            |> List.ofArray
        let visionYes = lines visionYesBox
        let visionNo = lines visionNoBox
        match visionYes @ visionNo |> List.filter (fun m -> not (List.contains m models)) with
        | m :: _ -> fail (if List.contains m visionYes then visionYesBox else visionNoBox) (sprintf "“%s”不在模型列表中，请先把它加到模型列表，或删掉这一行。" m)
        | [] ->
            match visionYes |> List.filter (fun m -> List.contains m visionNo) with
            | m :: _ -> fail visionNoBox (sprintf "“%s”同时写在两栏里，请只保留一栏。" m)
            | [] -> ()

        { id = id; label = label; url = url; models = models; defaultModel = defaultModel; timeout = timeout; retries = retries
          visionYes = visionYes; visionNo = visionNo; autoPicked = autoPicked; errors = List.ofSeq errors }
