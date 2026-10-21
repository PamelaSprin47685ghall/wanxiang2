namespace Wanxiang.UI

open System
open System.Text.Json
open System.Text.Json.Nodes

/// 一个分叉的谱系（根对话没有）。字段缺省 = 旧主机没发，界面退回“只显示分叉标记”。
type ForkLineage =
    {
        parentId: Guid
        /// 分叉点：父对话里最后一条被继承的消息（None = 从第一条消息处分出）
        forkBaseMessageId: uint64 option
        /// 分叉里自己记录的第一条消息（被编辑后的那条）
        forkFirstMessageId: uint64 option
        parentTitle: string option
        parentDeleted: bool
        /// 被编辑的是父对话的第几条消息（1 起）
        forkOrdinal: int option
    }

/// 顶栏面包屑。parentAvailable = false 时不可点击回到父对话（已删除）。
type ForkBreadcrumb =
    {
        parentId: Guid
        parentAvailable: bool
        text: string
        /// 无障碍与悬停提示
        description: string
    }

/// 同一条消息位置上的全部版本（原文 + 各分叉）。`index` 是 0 起的当前位置，界面显示 `index+1 / count`。
type SiblingVersions =
    {
        index: int
        count: int
        /// 与版本一一对应的对话 id
        targets: Guid list
    }

/// 一条全文搜索命中（`search.results.items` 的一项）。
/// 数字节点的宽容读取：线上解析出的节点与进程内用 int / int64 构造的节点都能读（后者 `TryGetValue<uint64>` 会失败）。
module internal JsonNumbers =
    let tryUInt64 (n: JsonNode) : uint64 option =
        match n with
        | :? JsonValue as v ->
            match v.TryGetValue<uint64>() with
            | true, value -> Some value
            | _ ->
                match v.TryGetValue<int64>() with
                | true, value when value >= 0L -> Some(uint64 value)
                | _ ->
                    match v.TryGetValue<int>() with
                    | true, value when value >= 0 -> Some(uint64 value)
                    | _ -> None
        | _ -> None

module ForkLineage =

    let private tryStr (o: JsonObject) key =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.String
        then
            Some(n.GetValue<string>())
        else
            None

    let private tryU64 (o: JsonObject) key =
        let mutable n: JsonNode = null

        if
            o.TryGetPropertyValue(key, &n)
            && not (isNull n)
            && n.GetValueKind() = JsonValueKind.Number
        then
            JsonNumbers.tryUInt64 n
        else
            None

    let private tryBool (o: JsonObject) key =
        let mutable n: JsonNode = null

        o.TryGetPropertyValue(key, &n)
        && not (isNull n)
        && n.GetValueKind() = JsonValueKind.True

    /// 解析快照里每一行的谱系；没有 `parentId`（根对话、旧主机）的行不入表。
    let parse (items: JsonArray) : Map<Guid, ForkLineage> =
        items
        |> Seq.choose (fun item ->
            match item with
            | :? JsonObject as o ->
                match
                    tryStr o "conversationId"
                    |> Option.bind (fun s ->
                        match Guid.TryParse s with
                        | true, g -> Some g
                        | _ -> None),
                    tryStr o "parentId"
                    |> Option.bind (fun s ->
                        match Guid.TryParse s with
                        | true, g -> Some g
                        | _ -> None)
                with
                | Some id, Some parentId ->
                    Some(
                        id,
                        {
                            parentId = parentId
                            forkBaseMessageId = tryU64 o "forkBaseMessageId"
                            forkFirstMessageId = tryU64 o "forkFirstMessageId"
                            parentTitle = tryStr o "parentTitle"
                            parentDeleted = tryBool o "parentDeleted"
                            forkOrdinal = tryU64 o "forkOrdinal" |> Option.map int
                        }
                    )
                | _ -> None
            | _ -> None)
        |> Map.ofSeq
