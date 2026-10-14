namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 客户端连接凭据的纯解析（决策 52/53/64、Q191），迁自旧 `CredentialStore.fs`。
/// 读 client.toml 文件、读写 IndexedDB、取页面地址都碰平台，归宿主；这里只把读到的文本变成连接。
/// 模块名与宿主的 CredentialStore 相同，F# 按名合并，旧调用方不用改。
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module CredentialStore =

    /// 桌面默认连接地址（同进程主机）。
    let desktopServerUrl = "ws://127.0.0.1:8765/ws"

    /// client.toml 的 url / token（`key = "value"` 或 `key="value"`）；两者都有才算一条连接。
    let parseClientToml (lines: string[]) : (string * string) option =
        let get (key: string) =
            lines
            |> Array.tryPick (fun line ->
                let t = line.Trim()
                if t.StartsWith(key + " =") || t.StartsWith(key + "=") then
                    let v = t.Substring(t.IndexOf '=' + 1).Trim().Trim('"')
                    if String.IsNullOrWhiteSpace v then None else Some v
                else None)
        match get "url", get "token" with
        | Some url, Some token -> Some(url, token)
        | _ -> None

    /// IndexedDB 连接记录（JSON 数组，宿主已按 updatedAt 降序）→ (url, token, instanceId)。
    /// 缺 url 或 token 的记录跳过；坏 JSON 当作没有记录。Q191：instanceId 才是身份，URL 不是。
    let parseConnections (json: string) : (string * string * string) list =
        try
            let results = Collections.Generic.List<string * string * string>()
            if not (String.IsNullOrWhiteSpace json) then
                match JsonNode.Parse json with
                | null -> ()
                | node when node.GetValueKind() = JsonValueKind.Array ->
                    for item in node.AsArray() do
                        if not (isNull item) && item.GetValueKind() = JsonValueKind.Object then
                            let o = item.AsObject()
                            let getStr (k: string) =
                                let mutable n: JsonNode = null
                                if o.TryGetPropertyValue(k, &n) && not (isNull n) then n.GetValue<string>() else ""
                            let instanceId = getStr "instanceId"
                            let url = getStr "url"
                            let token = getStr "token"
                            if not (String.IsNullOrWhiteSpace url) && not (String.IsNullOrWhiteSpace token) then
                                results.Add(url, token, instanceId)
                | _ -> ()
            List.ofSeq results
        with _ -> []

    /// 浏览器默认连接地址：与页面同源（https → wss，http → ws）。wsPath 是协议的 WebSocket 路径（ChatDomain 的 Constants.WsPath）。
    let serverUrlOfPage (wsPath: string) (href: string) : string =
        try
            let scheme =
                if href.StartsWith "https://" then "wss://"
                elif href.StartsWith "http://" then "ws://"
                else "ws://"
            // 用实际前缀长度切割（http:// 7 字符 / https:// 8 字符），避免 Substring 错位
            let rest = href.Substring(href.IndexOf("://", StringComparison.Ordinal) + 3)
            let host = rest.Split('/').[0]
            scheme + host + wsPath
        with _ ->
            desktopServerUrl
