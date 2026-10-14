namespace Wanxiang.UI

open System.Text.Json.Nodes

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs `ForkFrom`：
// forkAfterId 的计算与 editedMessage 的拼装逐行照抄；messages 换成 commitId 列表（旧代码只读 m.commitId）。
module ForkAnchor =
    let forkAfter (commitIds: uint64 list) (commitId: uint64 option) : uint64 option =
        let messages = commitIds |> List.map (fun id -> {| commitId = Some id |})
        let forkAfterId =
            match commitId with
            | Some target ->
                messages
                |> List.choose (fun m -> m.commitId)
                |> List.filter (fun id -> id < target)
                |> List.sortDescending
                |> List.tryHead
            | None -> messages |> List.choose (fun m -> m.commitId) |> List.sortDescending |> List.tryHead
        forkAfterId

    let editedMessage (edited: string) : JsonObject =
        let editedMessage = JsonObject()
        editedMessage["role"] <- "user"
        let contents = JsonArray()
        let textContent = JsonObject()
        textContent["text"] <- edited
        contents.Add textContent
        editedMessage["contents"] <- contents
        editedMessage

    let noConversationText = "当前没有可分叉的对话。"
