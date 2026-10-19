namespace Wanxiang.Server

open System
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes

/// 一个 MCP 工具的声明（名称 + 描述 + 入参 schema）。
type McpToolInfo = {
    name: string
    description: string
    /// MCP 服务给出的 inputSchema。转发给模型，模型才能填对参数。
    inputSchema: JsonNode option
}

/// MCP 协议的消息内容：握手参数、工具清单解析、工具调用的参数与结果。不碰传输。
module McpProtocol =

    /// MCP 协议版本（单一权威，P-002；version-authority.md 钉住）。
    [<Literal>]
    let ProtocolVersion = "2025-06-18"

    let initializeParams () : JsonObject =
        let p = JsonObject()
        p["protocolVersion"] <- ProtocolVersion
        let caps = JsonObject()
        caps["roots"] <- JsonObject()
        p["capabilities"] <- caps
        let info = JsonObject()
        info["name"] <- "wanxiang"
        info["version"] <- "1.0"
        p["clientInfo"] <- info
        p

    /// tools/list 的 result → 工具声明。没有名字的项丢弃；schema 不是对象时当作没有。
    let parseTools (result: JsonNode) : McpToolInfo list =
        match result with
        | :? JsonObject as ro ->
            let mutable toolsNode: JsonNode = null
            if ro.TryGetPropertyValue("tools", &toolsNode) && not (isNull toolsNode) && toolsNode.GetValueKind() = JsonValueKind.Array then
                [ for t in toolsNode.AsArray() do
                      match t with
                      | :? JsonObject as a ->
                          let str k =
                              let mutable n: JsonNode = null
                              if a.TryGetPropertyValue(k, &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.String then n.GetValue<string>() else ""
                          let name = str "name"
                          if not (String.IsNullOrWhiteSpace name) then
                              let schema =
                                  let mutable n: JsonNode = null
                                  if a.TryGetPropertyValue("inputSchema", &n) && not (isNull n) && n.GetValueKind() = JsonValueKind.Object then Some(n.DeepClone())
                                  else None
                              { name = name; description = str "description"; inputSchema = schema }
                      | _ -> () ]
            else []
        | _ -> []

    /// 模型给的参数文本。空白或 null 当作空对象；不是 JSON 时返回 None，工具不会被调用（Q170）。
    let parseArguments (argsJson: string) : JsonNode option =
        if String.IsNullOrWhiteSpace argsJson then Some(JsonObject() :> JsonNode)
        else
            try
                match JsonNode.Parse argsJson with
                | null -> Some(JsonObject() :> JsonNode)
                | node -> Some node
            with _ -> None

    /// 参数不是 JSON 时交给模型的结果文本。
    [<Literal>]
    let InvalidArguments = """{"error":"invalid arguments: not valid JSON"}"""

    let callParams (toolName: string) (args: JsonNode) : JsonObject =
        let p = JsonObject()
        p["name"] <- toolName
        p["arguments"] <- args
        p

    let private jsonOptions = JsonSerializerOptions(Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

    /// 工具调用的结果文本（交给模型）：成功是 result 原文，失败是 `{"error":...}`。中文不转义，模型读得懂。
    let renderCallResult (outcome: Result<JsonNode, string>) : string =
        match outcome with
        | Ok result -> result.ToJsonString(jsonOptions)
        | Error e ->
            let errObj = JsonObject()
            match (try Some(JsonNode.Parse e) with _ -> None) with
            | Some node -> errObj["error"] <- node
            | None -> errObj["error"] <- e
            errObj.ToJsonString(jsonOptions)
