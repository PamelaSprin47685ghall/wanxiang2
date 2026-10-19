namespace Wanxiang2.WorkspaceState

open System

/// 决策 231：单条消息的可复制链接解析。
module MessageLink =

    let scheme = "wanxiang://"

    let private query (conversationId: Guid) (commitId: uint64) =
        sprintf "open?c=%s&m=%d" (conversationId.ToString "D") commitId

    /// pageUrl：PWA 当前页面地址（去掉原有 #片段）；桌面传 None。
    let format (pageUrl: string option) (conversationId: Guid) (commitId: uint64) : string =
        match pageUrl |> Option.filter (fun u -> u.StartsWith "http://" || u.StartsWith "https://") with
        | Some url ->
            let baseUrl = match url.IndexOf '#' with -1 -> url | i -> url.Substring(0, i)
            baseUrl + "#" + query conversationId commitId
        | None -> scheme + query conversationId commitId

    /// 认出任一写法；不是消息链接返回 None（普通搜索词照常搜索）。
    let tryParse (text: string) : (Guid * uint64) option =
        if String.IsNullOrWhiteSpace text then None
        else
            let t = text.Trim()
            let start =
                if t.StartsWith(scheme, StringComparison.OrdinalIgnoreCase) then Some(scheme.Length)
                elif t.StartsWith "http://" || t.StartsWith "https://" then
                    match t.IndexOf "#open?" with
                    | -1 -> None
                    | i -> Some(i + 1)
                else None
            match start with
            | None -> None
            | Some s ->
                let rest = t.Substring s
                if not (rest.StartsWith "open?") then None
                else
                    let pairs =
                        rest.Substring(5).Split('&')
                        |> Array.choose (fun kv ->
                            match kv.IndexOf '=' with
                            | -1 -> None
                            | i -> Some(kv.Substring(0, i), Uri.UnescapeDataString(kv.Substring(i + 1))))
                        |> Map.ofArray
                    match pairs.TryFind "c", pairs.TryFind "m" with
                    | Some c, Some m ->
                        match Guid.TryParse c, UInt64.TryParse m with
                        | (true, g), (true, n) when g <> Guid.Empty -> Some(g, n)
                        | _ -> None
                    | _ -> None
