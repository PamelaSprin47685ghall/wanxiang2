namespace Wanxiang.Server

open System
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.AI
open Wanxiang.Config
open Wanxiang.Core

/// 把一个 MCP 工具包装成模型可调用的 `AIFunction`，并**原样转发**
/// MCP 服务给出的 `inputSchema`。
///
/// 旧实现把每个 MCP 工具都注册成 `Func<string, ...>`，模型只看到一个
/// 叫 `args` 的字符串参数、以及一句「MCP tool x/y」的假描述——
/// 结果是模型几乎不可能正确调用任何 MCP 工具。
type McpFunction(client: McpClient, serverId: string, info: McpToolInfo) =
    inherit AIFunction()

    /// 无 inputSchema 时给一个接受任意对象的兜底 schema。
    static let permissiveSchema =
        JsonDocument.Parse("""{"type":"object","additionalProperties":true}""").RootElement.Clone()

    let schema =
        match info.inputSchema with
        | Some node ->
            try
                JsonDocument.Parse(node.ToJsonString()).RootElement.Clone()
            with _ ->
                permissiveSchema
        | None -> permissiveSchema

    let functionName = ToolNames.mcpFunctionName serverId info.name

    override _.Name = functionName

    /// 稳定标识（`mcp:<server>/<tool>`），审批策略按它查。
    member _.StableId = ToolNames.mcpStableId serverId info.name

    override _.Description =
        if String.IsNullOrWhiteSpace info.description then
            sprintf "%s 提供的工具 %s。" serverId info.name
        else
            info.description

    override _.JsonSchema = schema

    override _.InvokeCoreAsync(args: AIFunctionArguments, ct: CancellationToken) : ValueTask<obj> =
        ValueTask<obj>(
            task {
                // 模型给的参数是键值对，MCP 需要一个 JSON 对象
                let payload = JsonObject()

                for KeyValue(key, value) in args do
                    payload[key] <-
                        match value with
                        | null -> null
                        | :? JsonNode as node -> node.DeepClone()
                        | :? JsonElement as element -> JsonNode.Parse(element.GetRawText())
                        | other ->
                            try
                                JsonSerializer.SerializeToNode(other, AIJsonUtilities.DefaultOptions)
                            with _ ->
                                JsonNode.op_Implicit (string other)

                let! result = client.Call(info.name, payload.ToJsonString(), ct)
                return box result
            }
        )

/// 一个工具在界面上的样子。
type ToolDescriptor =
    {
        /// 稳定标识（对话配置里存的就是它）
        id: string
        /// 模型看到的函数名
        functionName: string
        label: string
        description: string
        /// "builtin" | "mcp"
        source: string
        /// MCP 工具所属 MCP 服务 id
        serverId: string option
    }

/// 工具注册表：内置工具 + MCP 工具（决策 95-100）。
/// 对话配置只保存稳定标识（`builtin:...` / `mcp:<server-id>/<tool-name>`）。
type ToolRegistry(getConfig: unit -> AppConfig, onLog: string -> unit) =

    /// MCP 客户端按服务 id 缓存；配置指纹变化时旧客户端排空关闭、新建（决策 98）。
    let mcpClients = RefreshingPool<McpClient>(fun c -> c.Stop())

    let fingerprint (cfg: McpServerConfig) : string =
        sprintf
            "%s|%A|%A|%s|%A|%d"
            (cfg.command |> Option.defaultValue "")
            cfg.args
            cfg.env
            (cfg.url |> Option.defaultValue "")
            cfg.maxConcurrency
            cfg.callTimeoutSeconds

    let mcpServers () = (getConfig ()).mcpServers

    member _.GetMcpClient(id: string) : McpClient option =
        let wanted =
            match mcpServers () |> Map.tryFind id with
            | Some cfg when cfg.enabled -> Some(fingerprint cfg, (fun () -> McpClient(cfg, onLog)))
            // 配置已删除或停用：排空关闭旧客户端（决策 98）
            | _ -> None

        mcpClients.Acquire(id, wanted)

    /// 已缓存客户端里对应配置消失的那些：排空关闭，避免孤儿子进程。
    member _.PruneRemoved() : unit =
        let live = mcpServers ()

        mcpClients.Prune(fun id ->
            match live |> Map.tryFind id with
            | Some cfg -> cfg.enabled
            | None -> false)

    /// 全部可选工具（内置 + 已连上的 MCP 服务）。客户端据此渲染工具清单。
    member this.Descriptors() : ToolDescriptor list =
        let cfg = getConfig ()

        let builtins =
            BuiltinTools.descriptors cfg.tools
            |> List.filter (fun (id, _, _) -> cfg.tools.exposeDebug || not (BuiltinTools.isDebug id))
            |> List.map (fun (id, fn, desc) ->
                {
                    id = id
                    functionName = fn
                    label = id.Substring("builtin:".Length)
                    description = desc
                    source = "builtin"
                    serverId = None
                })

        let mcp =
            cfg.mcpServers
            |> Map.toList
            |> List.map snd
            |> List.filter (fun s -> s.enabled)
            |> List.sortBy (fun s -> s.id)
            |> List.collect (fun server ->
                match this.GetMcpClient server.id with
                | None -> []
                | Some client ->
                    client.Tools()
                    |> List.map (fun info ->
                        {
                            id = ToolNames.mcpStableId server.id info.name
                            functionName = ToolNames.mcpFunctionName server.id info.name
                            label = info.name
                            description = info.description
                            source = "mcp"
                            serverId = Some server.id
                        }))

        builtins @ mcp

    /// 目录快照用的 JSON 形态。
    member this.DescriptorsJson() : JsonArray =
        let arr = JsonArray()

        for d in this.Descriptors() do
            let o = JsonObject()
            o["id"] <- d.id
            o["functionName"] <- d.functionName
            o["label"] <- d.label
            o["description"] <- d.description
            o["source"] <- d.source

            match d.serverId with
            | Some s -> o["serverId"] <- s
            | None -> ()

            arr.Add o

        arr

    /// 构建对话启用的 AITool 列表。
    member this.BuildTools(config: SessionConfig) : AITool list =
        let appCfg = getConfig ()
        let requested = config.tools |> Set.ofList

        let builtins =
            BuiltinTools.all appCfg.tools
            |> List.filter (fun t -> requested.Contains(BuiltinTools.stableId t.Name))

        let mcpTools =
            requested
            |> Seq.choose ToolNames.parseMcpId
            |> Seq.distinct
            |> Seq.choose (fun (serverId, toolName) ->
                match this.GetMcpClient serverId with
                | None -> None
                | Some client ->
                    match client.TryTool toolName with
                    | Some info -> Some(McpFunction(client, serverId, info) :> AITool)
                    | None ->
                        onLog (sprintf "[mcp:%s] 对话请求的工具 %s 不存在，已跳过" serverId toolName)
                        None)
            |> List.ofSeq

        builtins @ mcpTools

    /// 一个工具的稳定标识（审批策略的键）。
    member _.StableIdOf(tool: AITool) : string =
        match tool with
        | :? McpFunction as m -> m.StableId
        | t -> BuiltinTools.stableId t.Name

    /// 对话请求了但当前不可用的工具（供 UI 提示配置漂移）。
    member this.MissingTools(config: SessionConfig) : string list =
        // 调试工具默认不进目录，但老对话已经选了它时仍然可用，不能算「配置漂移」
        let available =
            (this.Descriptors() |> List.map (fun d -> d.id))
            @ (BuiltinTools.descriptors (getConfig ()).tools |> List.map (fun (id, _, _) -> id))
            |> Set.ofList

        config.tools |> List.filter (fun t -> not (available.Contains t))

    /// 关闭全部 MCP 客户端（排空后关闭，决策 98）。
    member _.Dispose() = mcpClients.StopAll()
