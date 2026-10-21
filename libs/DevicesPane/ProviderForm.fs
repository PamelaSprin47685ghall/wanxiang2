namespace Wanxiang.UI

open System
open System.Text.RegularExpressions

/// 模型服务编辑器的字段（泛名，限定访问）
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

/// 编辑器里的原始文本（url 由调用方先用 ProviderPresets.normalizeBaseUrl 归一）
type ProviderFormInput =
    {
        isNew: bool
        takenIds: string list
        /// 编辑已有服务时原来的超时与重试，留空即沿用
        existing: (int * int) option
        id: string
        label: string
        url: string
        models: string
        defaultModel: string
        timeout: string
        retries: string
        visionYes: string
        visionNo: string
    }

/// 校验结果。errors 为空即可保存；autoPicked 为自动选用的默认模型（要回填输入框并提示）
type ProviderFormResult =
    {
        id: string
        label: string
        url: string
        models: string list
        defaultModel: string
        timeout: int
        retries: int
        visionYes: string list
        visionNo: string list
        autoPicked: string option
        errors: (ProviderField * string) list
    }

module ProviderForm =

    let private trim (s: string) = if isNull s then "" else s.Trim()

    /// 模型名列表：按换行或逗号切，去空白，去重，保持次序
    let lines (text: string) =
        (if isNull text then "" else text).Split([| '\n'; '\r'; ',' |], StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.distinct
        |> List.ofArray

    /// 自动选用默认模型时的提示
    let autoPickedNote (model: string) = sprintf "未指定默认模型，已自动选用“%s”。" model

    /// 所有 loopback 写法都算本机：Uri.IsLoopback 加子串回退
    let isLoopbackUrl (baseUrl: string) =
        if String.IsNullOrWhiteSpace baseUrl then
            false
        else
            match Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute) with
            | true, uri when uri.IsLoopback -> true
            | _ ->
                let lower = baseUrl.Trim().ToLowerInvariant()

                lower.Contains("127.0.0.1")
                || lower.Contains("localhost")
                || lower.Contains("[::1]")
                || lower.Contains("::1")

    /// 旧 SettingsProviders 编辑器 saveCore 的校验，错误按旧顺序给出
    let validate (i: ProviderFormInput) : ProviderFormResult =
        let id, label, url = trim i.id, trim i.label, i.url
        let models = lines i.models
        let rawDefault = trim i.defaultModel
        let errs = ResizeArray<ProviderField * string>()
        let fail f m = errs.Add(f, m)

        if String.IsNullOrWhiteSpace id then
            fail ProviderField.Id "稳定标识不能为空。"
        elif i.isNew && List.contains id i.takenIds then
            fail ProviderField.Id "该标识已存在，请换一个。"
        elif not (Regex.IsMatch(id, "^[a-zA-Z0-9_-]+$")) then
            fail ProviderField.Id "稳定标识仅支持英文字母、数字、下划线(_)与连字符(-)。"

        if String.IsNullOrWhiteSpace label then
            fail ProviderField.Label "显示名称不能为空。"

        if String.IsNullOrWhiteSpace url then
            fail ProviderField.Url "端点地址不能为空。"
        elif
            not (
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            )
        then
            fail ProviderField.Url "端点地址必须以 http:// 或 https:// 开头。"
        else
            match Uri.TryCreate(url, UriKind.Absolute) with
            | true, uri when uri.Scheme = "http" || uri.Scheme = "https" -> ()
            | _ -> fail ProviderField.Url "请输入合法的 HTTP/HTTPS 地址（例如 https://api.openai.com/v1）。"

        if List.isEmpty models then
            fail ProviderField.Models "至少填写一个模型名（每行一个）。"

        let defaultModel, autoPicked =
            if String.IsNullOrWhiteSpace rawDefault then
                match models with
                | head :: _ -> head, Some head
                | [] -> "", None
            else
                if not (List.isEmpty models) && not (List.contains rawDefault models) then
                    fail ProviderField.DefaultModel (sprintf "默认模型“%s”不在模型列表中，请从列表中选择或留空自动选用。" rawDefault)

                rawDefault, None

        let timeout =
            let raw = trim i.timeout

            if String.IsNullOrWhiteSpace raw then
                i.existing |> Option.map fst |> Option.defaultValue 120
            else
                match Int32.TryParse raw with
                | true, v when v > 0 -> v
                | _ ->
                    fail ProviderField.Timeout "超时时间必须是大于 0 的整秒数。"
                    120

        let retries =
            let raw = trim i.retries

            if String.IsNullOrWhiteSpace raw then
                i.existing |> Option.map snd |> Option.defaultValue 2
            else
                match Int32.TryParse raw with
                | true, v when v >= 0 && v <= 10 -> v
                | _ ->
                    fail ProviderField.Retries "重试次数必须是 0 到 10 之间的整数。"
                    2

        let yes, no = lines i.visionYes, lines i.visionNo

        match yes @ no |> List.filter (fun m -> not (List.contains m models)) with
        | m :: _ ->
            fail
                (if List.contains m yes then
                     ProviderField.VisionYes
                 else
                     ProviderField.VisionNo)
                (sprintf "“%s”不在模型列表中，请先把它加到模型列表，或删掉这一行。" m)
        | [] ->
            match yes |> List.filter (fun m -> List.contains m no) with
            | m :: _ -> fail ProviderField.VisionNo (sprintf "“%s”同时写在两栏里，请只保留一栏。" m)
            | [] -> ()

        {
            id = id
            label = label
            url = url
            models = models
            defaultModel = defaultModel
            timeout = timeout
            retries = retries
            visionYes = yes
            visionNo = no
            autoPicked = autoPicked
            errors = List.ofSeq errs
        }
