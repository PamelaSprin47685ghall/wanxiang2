namespace Wanxiang.UI

open System
open System.Text.Json.Nodes

// 组合者：主机目录（HostCatalog）与看图判断（ModelVision）、会话配置（Wire）、模型切换（ModelSwitcher）的关联。
// 模块名与库里相同，F# 按名合并同名模块，旧调用方不用改。

module Catalog =

    /// 决策 330/331：某模型能否看图。用户指定 > 模型服务信息 > 名字推断。
    let visionOf (providerId: string) (model: string) (catalog: Catalog) : VisionSupport * VisionSource =
        let ofBool b = if b then VisionYes else VisionNo
        match Catalog.tryProvider providerId catalog with
        | Some p when p.visionOverrides.ContainsKey model -> ofBool p.visionOverrides[model], VisionFromUser
        | Some p when p.visionDetected.ContainsKey model -> ofBool p.visionDetected[model], VisionFromProvider
        | _ -> ModelVision.classify model, VisionFromName


/// 对话预设的纯逻辑（决策 433–436）：套用、另存、描述。界面只负责画。
module Presets =
    open Wanxiang.Core

    /// 套用预设得到的会话配置。预设里的模型服务不可用（被删、停用、没有该模型）时
    /// 回落到新对话默认模型，并返回 Some 提示文案；不可用的工具静默剔除并计入提示。
    let toSessionConfig (catalog: Catalog) (preset: PresetInfo) : SessionConfig * string option =
        let usable = Catalog.usableProviders catalog
        let requested =
            if String.IsNullOrWhiteSpace preset.provider then None
            else usable |> List.tryFind (fun p -> p.id = preset.provider)
        let provider, model, modelNote =
            match requested with
            | Some p ->
                let model =
                    if not (String.IsNullOrWhiteSpace preset.model) && List.contains preset.model p.models then preset.model
                    elif not (String.IsNullOrWhiteSpace p.defaultModel) then p.defaultModel
                    else p.models |> List.tryHead |> Option.defaultValue ""
                let note =
                    if not (String.IsNullOrWhiteSpace preset.model) && model <> preset.model then
                        Some(sprintf "预设里的模型 %s 已不可用，改用 %s。" preset.model model)
                    else None
                p.id, model, note
            | None ->
                let p, m = Catalog.defaultSelection catalog |> Option.defaultValue ("", "")
                let note =
                    if String.IsNullOrWhiteSpace preset.provider then None
                    else Some(sprintf "预设里的模型服务已不可用，改用默认模型 %s。" (Catalog.describeModel p m catalog))
                p, m, note
        let available = catalog.tools |> List.map (fun t -> t.id) |> Set.ofList
        let tools = preset.tools |> List.filter available.Contains
        let dropped = List.length preset.tools - List.length tools
        // 决策 447：点名被跳过的工具（去掉命名空间前缀），用户才知道少了什么。
        let missing =
            preset.tools
            |> List.filter (fun t -> not (available.Contains t))
            |> List.map (fun t -> match t.LastIndexOf ':' with -1 -> t | i -> t.Substring(i + 1))
        let toolNote =
            if dropped = 0 then None
            elif dropped <= 3 then Some(sprintf "工具 %s 在主机上已没有，已跳过。" (String.Join("、", missing)))
            else Some(sprintf "有 %d 个工具主机上已没有，已跳过。" dropped)
        let note =
            match [ modelNote; toolNote ] |> List.choose id with
            | [] -> None
            | parts -> Some(String.concat "" parts)
        { SessionConfig.empty with
            provider = provider
            model = model
            instructions = preset.instructions |> Option.orElse catalog.generation.instructions
            tools = tools
            temperature = preset.temperature |> Option.orElse catalog.generation.temperature
            topP = preset.topP |> Option.orElse catalog.generation.topP
            maxTokens = preset.maxTokens |> Option.orElse catalog.generation.maxTokens
            thinkingBudget = preset.thinkingBudget },
        note

    /// 「存为预设」的上行负载：取对话当前的完整会话配置。id 缺省 = 新建。
    let payloadOf (id: string option) (name: string) (config: SessionConfig) : JsonObject =
        let o = JsonObject()
        id |> Option.iter (fun i -> o["id"] <- i)
        o["name"] <- name.Trim()
        o["provider"] <- config.provider
        o["model"] <- config.model
        config.instructions |> Option.iter (fun i -> o["instructions"] <- i)
        o["tools"] <- JsonArray([| for t in config.tools -> JsonNode.op_Implicit t |])
        config.temperature |> Option.iter (fun x -> o["temperature"] <- x)
        config.topP |> Option.iter (fun x -> o["topP"] <- x)
        config.maxTokens |> Option.iter (fun x -> o["maxTokens"] <- x)
        config.thinkingBudget |> Option.iter (fun x -> o["thinkingBudget"] <- x)
        o

    let ofPreset (p: PresetInfo) : SessionConfig =
        { SessionConfig.empty with
            provider = p.provider; model = p.model; instructions = p.instructions; tools = p.tools
            temperature = p.temperature; topP = p.topP; maxTokens = p.maxTokens; thinkingBudget = p.thinkingBudget }

    /// 一行说明：模型 · 指令首句 · 工具数，用在命令面板副标题和设置列表。
    let describe (catalog: Catalog) (p: PresetInfo) : string =
        let model =
            if String.IsNullOrWhiteSpace p.provider then "默认模型"
            else Catalog.describeModel p.provider p.model catalog
        let instr =
            p.instructions
            |> Option.map (fun i ->
                let line = i.Split('\n').[0].Trim()
                if line.Length > 24 then line.Substring(0, 24) + "…" else line)
        let tools = if List.isEmpty p.tools then None else Some(sprintf "%d 个工具" p.tools.Length)
        String.Join(" · ", [ Some model; instr; tools ] |> List.choose id)

    /// 新名字的建议：对话标题，重名时补序号。
    let suggestName (existing: PresetInfo list) (title: string) : string =
        let baseName =
            let t = if String.IsNullOrWhiteSpace title || title = "新对话" then "我的预设" else title.Trim()
            if t.Length > 30 then t.Substring(0, 30) else t
        let taken = existing |> List.map (fun p -> p.name.ToLowerInvariant()) |> Set.ofList
        if not (taken.Contains(baseName.ToLowerInvariant())) then baseName
        else
            Seq.initInfinite (fun i -> sprintf "%s %d" baseName (i + 2))
            |> Seq.find (fun n -> not (taken.Contains(n.ToLowerInvariant())))

module ModelSwitcher =

    /// 所有可用模型，按模型服务的配置顺序、服务内模型顺序。
    let options (catalog: Catalog) (current: (string * string) option) : ModelOption list =
        let defaultSel = Catalog.defaultSelection catalog
        [ for p in Catalog.usableProviders catalog do
              for m in p.models ->
                  { providerId = p.id
                    providerLabel = p.label
                    model = m
                    isCurrent = current = Some(p.id, m)
                    isDefault = defaultSel = Some(p.id, m) } ]

    /// 顶栏芯片文字：模型名（短），服务名放第二层（提示与读屏）。
    let chipText (providerId: string) (model: string) (catalog: Catalog) : string =
        if String.IsNullOrWhiteSpace model then Catalog.describeModel providerId model catalog else model
