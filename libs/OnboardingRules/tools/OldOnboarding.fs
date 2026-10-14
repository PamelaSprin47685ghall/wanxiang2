namespace Wanxiang.UI

open System
open System.Net.Http
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading.Tasks

module Onboarding =

    /// 「试一句」预填的第一句话。
    [<Literal>]
    let FirstPrompt = "你好，介绍一下你自己"

    /// 解析 OpenAI 兼容 `/models` 响应（`{data:[{id}]}`）。
    let parseModelIds (body: string) : string list =
        try
            match JsonNode.Parse body with
            | :? JsonObject as root ->
                match root["data"] with
                | :? JsonArray as items ->
                    [ for item in items do
                          match item with
                          | :? JsonObject as o ->
                              match o["id"] with
                              | null -> ()
                              | v when v.GetValueKind() = JsonValueKind.String -> v.GetValue<string>()
                              | _ -> ()
                          | _ -> () ]
                    |> List.filter (String.IsNullOrWhiteSpace >> not)
                    |> List.distinct
                | _ -> []
            | _ -> []
        with _ -> []

    /// 探测本机 Ollama（只在 loopback 上发一次 GET，短超时；失败一律视为没有）。
    /// 这是用户已确认允许的本机端口探测：不带任何凭据，不写任何文件。
    let discoverOllamaAt (url: string) : Task<string list option> =
        task {
            try
                use client = new HttpClient(Timeout = TimeSpan.FromMilliseconds 1500.0)
                use! response = client.GetAsync url
                if response.IsSuccessStatusCode then
                    let! body = response.Content.ReadAsStringAsync()
                    match parseModelIds body with
                    | [] -> return None
                    | ids -> return Some ids
                else
                    return None
            with _ -> return None
        }

    /// 只有主机在本机（loopback）时，「本机 Ollama」才是主机会用到的那个 Ollama。
    /// 浏览器（PWA）里既没有本机访问权，也不该替远端主机猜。
    let canDiscoverLocal (serverUrl: string) =
        not (OperatingSystem.IsBrowser())
        && (match Uri.TryCreate(serverUrl, UriKind.Absolute) with
            | true, uri -> uri.IsLoopback
            | _ -> false)

    let pickDefaultModel (presetModels: string list) (probed: string list) : string =
        presetModels
        |> List.tryFind (fun m -> List.contains m probed)
        |> Option.orElse (List.tryHead probed)
        |> Option.defaultValue (presetModels |> List.tryHead |> Option.defaultValue "")
