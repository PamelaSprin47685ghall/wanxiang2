namespace Wanxiang.Client

open System.Collections.Generic
open System.Text.Json.Nodes

/// 客户端消息列表（JsonArray）按 commitId 的操作（从旧 ClientState 拆出）。
/// commitId 是消息的唯一标识：重复推送、快照与 catch-up 交叠时都按它去重。
/// 非对象项与没有 commitId 的项原样保留、不参与匹配。
module CommitTimeline =

    /// 项的 commitId；不是对象或没有该字段时为 None。
    let commitIdOf (node: JsonNode) : uint64 option =
        match node with
        | :? JsonObject as o ->
            let mutable c: JsonNode = null
            if o.TryGetPropertyValue("commitId", &c) && not (isNull c) then Some(c.GetValue<uint64>()) else None
        | _ -> None

    let contains (commitId: uint64) (messages: JsonArray) : bool =
        messages |> Seq.exists (fun m -> commitIdOf m = Some commitId)

    /// 去掉 commitId 等于给定值的项，返回新数组（其余项深拷贝，顺序不变）。
    let without (commitId: uint64) (messages: JsonArray) : JsonArray =
        let remaining = JsonArray()
        for m in messages do
            if commitIdOf m <> Some commitId then
                remaining.Add(if isNull m then null else m.DeepClone())
        remaining

    /// 把更早的一页前置：只取 commitId > 0 且现有列表里没有的项。
    /// 返回 (合并后的新数组, 前置项里第一条的 commitId)。
    let prependOlder (older: JsonArray) (messages: JsonArray) : JsonArray * uint64 option =
        let existing = HashSet<uint64>()
        for m in messages do
            if not (isNull m) && m.GetValueKind() = System.Text.Json.JsonValueKind.Object then
                commitIdOf m |> Option.iter (existing.Add >> ignore)
        let prev = ResizeArray<JsonNode>()
        for m in older do
            let c = commitIdOf m |> Option.defaultValue 0UL
            if c > 0UL && not (existing.Contains c) then prev.Add(m.DeepClone())
        let combined = JsonArray()
        for m in prev do combined.Add m
        for m in messages do combined.Add(if isNull m then null else m.DeepClone())
        combined, (if prev.Count > 0 then commitIdOf prev[0] else None)
