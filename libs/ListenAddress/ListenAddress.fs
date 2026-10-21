namespace Wanxiang.Server

open System

/// 监听地址（`host:port`）是否只在本机回环上：127.0.0.0/8、::1、localhost。只依赖 BCL。从旧 ServerApp 拆出。
module ListenAddress =

    /// 取 `host:port` 的主机部分（最后一个冒号之前；没有冒号就是整串）。
    let hostOf (listen: string) : string =
        let trimmed = if isNull listen then "" else listen.Trim()

        match trimmed.LastIndexOf ':' with
        | -1 -> trimmed
        | index -> trimmed.Substring(0, index)

    let isLoopback (listen: string) : bool =
        let bare = (hostOf listen).Trim('[', ']')

        bare = "127.0.0.1"
        || bare = "::1"
        || bare.StartsWith("127.", StringComparison.Ordinal)
        || bare.Equals("localhost", StringComparison.OrdinalIgnoreCase)
