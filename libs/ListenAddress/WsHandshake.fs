namespace WsHandshake

open System
open System.Net

/// 浏览器 WebSocket 握手的来源检查（防跨站劫持与 DNS 重绑定）。纯函数。
[<RequireQualifiedAccess>]
module WsHandshake =
    /// Origin 检查：没有 Origin（原生客户端）放行；有则必须是 http(s)、没有路径和用户信息，
    /// 且 host:port 与 Host 头完全一致（同源）。"null" 或解析失败拒绝。
    let isAllowedOrigin (origin: string) (hostHeader: string) : bool =
        if String.IsNullOrEmpty origin then true
        elif String.IsNullOrWhiteSpace hostHeader then false
        else
            match Uri.TryCreate(origin, UriKind.Absolute) with
            | true, u when (u.Scheme = "http" || u.Scheme = "https") && u.AbsolutePath = "/" && String.IsNullOrEmpty u.UserInfo ->
                let defaultPort = if u.Scheme = "https" then 443 else 80
                let originAuthority = if u.Port = defaultPort then u.Host else sprintf "%s:%d" u.Host u.Port
                let host = hostHeader.Trim().ToLowerInvariant()
                let suffix = sprintf ":%d" defaultPort
                let host = if host.EndsWith suffix then host.Substring(0, host.Length - suffix.Length) else host
                String.Equals(originAuthority.ToLowerInvariant(), host, StringComparison.Ordinal)
            | _ -> false

    /// Host 白名单：IP 字面量、localhost，以及 extraAllowed 里登记的主机名（反向代理域名）。
    let isAllowedHost (hostHeader: string) (extraAllowed: string list) : bool =
        if String.IsNullOrWhiteSpace hostHeader then false
        else
            let h = hostHeader.Trim().ToLowerInvariant()
            let name =
                if h.StartsWith "[" then (match h.IndexOf ']' with | i when i > 0 -> h.Substring(1, i - 1) | _ -> h)
                else (match h.LastIndexOf ':' with | i when i > 0 && h.IndexOf ':' = i -> h.Substring(0, i) | _ -> h)
            name = "localhost"
            || (match IPAddress.TryParse name with | true, _ -> true | _ -> false)
            || extraAllowed |> List.exists (fun a -> String.Equals(a.Trim(), name, StringComparison.OrdinalIgnoreCase))
