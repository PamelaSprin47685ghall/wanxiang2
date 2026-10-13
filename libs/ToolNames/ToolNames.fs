namespace Wanxiang.Server

open System

/// 工具的稳定标识（对话配置里存的）与模型看到的函数名之间的换算。只依赖 BCL。
/// 内置：`builtin:file.read` ⇄ `builtin_file_read`；MCP：`mcp:<server>/<tool>`，函数名 `mcp_<server>_<tool>`。
module ToolNames =

    let builtinFunctionName (stableId: string) : string =
        "builtin_" + stableId.Substring("builtin:".Length).Replace(".", "_")

    let builtinStableId (functionName: string) : string =
        "builtin:" + functionName.Substring("builtin_".Length).Replace("_", ".")

    let mcpStableId (serverId: string) (toolName: string) : string = sprintf "mcp:%s/%s" serverId toolName

    let mcpFunctionName (serverId: string) (toolName: string) : string = sprintf "mcp_%s_%s" serverId toolName

    /// 解析 `mcp:<server>/<tool>`；第一个 `/` 分开服务与工具。
    let parseMcpId (id: string) : (string * string) option =
        if id.StartsWith("mcp:", StringComparison.Ordinal) then
            let rest = id.Substring 4
            match rest.IndexOf '/' with
            | -1 -> None
            | i -> Some(rest.Substring(0, i), rest.Substring(i + 1))
        else
            None
