namespace Wanxiang.Server

open System

/// 诊断检查「provider-config」：启用的模型服务必须有 apiKey 与 baseUrl（从旧 Doctor 拆出）。
module ProviderCheck =

    /// 输入 (id, enabled, hasApiKey, baseUrl)，按给定顺序；返回 (ok, detail)。
    let run (providers: (string * bool * bool * string) list) : bool * string =
        let issues =
            providers
            |> List.collect (fun (pid, enabled, hasKey, baseUrl) ->
                let problems =
                    [
                        if enabled && not hasKey then
                            Some "enabled but no apiKey"
                        else
                            None
                        if enabled && String.IsNullOrWhiteSpace baseUrl then
                            Some "enabled but empty baseUrl"
                        else
                            None
                    ]
                    |> List.choose id

                if List.isEmpty problems then
                    []
                else
                    [ sprintf "%s: %s" pid (String.concat "; " problems) ])

        List.isEmpty issues,
        (if List.isEmpty issues then
             "ok"
         else
             String.concat "; " issues)
