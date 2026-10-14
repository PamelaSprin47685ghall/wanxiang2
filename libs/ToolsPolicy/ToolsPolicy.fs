namespace Wanxiang.UI

open System.Text.Json.Nodes

/// 旧 Views/SettingsTools.fs 的工具设置载荷与策略文案
module ToolsPolicy =

    /// 策略的界面文案；未知值按自动执行显示
    let label (policy: string) =
        match policy with
        | "first" -> "首次询问"
        | "always" -> "每次询问"
        | _ -> "自动执行"

    /// 工具设置写入载荷（走 updateGeneration，主机缺省字段保持原值）
    let payload (roots: string list) (policyDefault: string) (policy: Map<string, string>) (exposeDebug: bool) =
        let tools = JsonObject()
        tools["fileReadRoots"] <- JsonArray([| for r in roots -> JsonNode.op_Implicit r |])
        let pol = JsonObject()
        pol["default"] <- policyDefault
        for KeyValue(id, p) in policy do pol[id] <- p
        tools["policy"] <- pol
        tools["exposeDebug"] <- exposeDebug
        let root = JsonObject()
        root["tools"] <- tools
        root
