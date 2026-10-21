namespace Wanxiang2.WorkspaceState

open System.Text.Json.Nodes

/// 「编辑并分叉」的客户端事实：分叉点选哪条已提交消息、被编辑的消息怎样拼成上线的 JSON。
module ForkAnchor =

    /// 分叉点：指定了消息（target）时取它之前（commitId 更小）的最后一条；没指定时取最后一条。
    /// 没有已提交消息时为 None（从头分叉）。commitIds 是当前对话各消息的 commitId（未提交的不在内）。
    let forkAfter (commitIds: uint64 list) (target: uint64 option) : uint64 option =
        let candidates =
            match target with
            | Some t -> commitIds |> List.filter (fun id -> id < t)
            | None -> commitIds

        candidates |> List.sortDescending |> List.tryHead

    /// 被编辑后的用户消息：{"role":"user","contents":[{"text":...}]}，字节与旧代码一致。
    let editedMessage (text: string) : JsonObject =
        let message = JsonObject()
        message["role"] <- "user"
        let contents = JsonArray()
        let textContent = JsonObject()
        textContent["text"] <- text
        contents.Add textContent
        message["contents"] <- contents
        message

    let noConversationText = "当前没有可分叉的对话。"
