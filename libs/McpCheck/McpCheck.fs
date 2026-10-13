namespace Wanxiang.Server

/// 诊断检查「mcp-config」：启用的 stdio 服务（只有 command、没有 url）其命令文件必须存在（从旧 Doctor 拆出）。
module McpCheck =

    /// 输入 (id, enabled, command, url)；`fileExists` 由调用方给出（磁盘探测归调用方）。返回 (ok, detail)。
    let run (fileExists: string -> bool) (servers: (string * bool * string option * string option) list) : bool * string =
        let issues =
            servers
            |> List.collect (fun (id, enabled, command, url) ->
                match command, url with
                | Some cmd, None when enabled && not (fileExists cmd) -> [ sprintf "%s: enabled command not found: %s" id cmd ]
                | _ -> [])
        List.isEmpty issues, (if List.isEmpty issues then "ok" else String.concat "; " issues)
