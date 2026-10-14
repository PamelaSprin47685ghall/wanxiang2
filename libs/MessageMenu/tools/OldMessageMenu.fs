namespace Wanxiang.UI

// 照抄旧 Views/MessageCard.fs messageMenuEntries；MenuEntry 换成 (项, 文案, 提示, 危险) 再映射回新形状
open System

[<RequireQualifiedAccess>]
type MessageMenuItem =
    | Copy
    | EditAndFork
    | Regenerate
    | CopyLink of commitId: uint64
    | Delete of commitId: uint64

type private E = { item: MessageMenuItem; label: string; hint: string; danger: bool }

module MessageMenu =
    let private create item label = { item = item; label = label; hint = ""; danger = false }
    let private withHint h e = { e with hint = h }
    let private asDanger e = { e with danger = true }

    let private old (streaming: bool) (isUser: bool) (isLastAssistant: bool) (text: string) (commitIdOpt: uint64 option) (hasLinkSink: bool) =
        let linkSink = if hasLinkSink then Some ignore else None
        if streaming then []
        else
            [ if not (String.IsNullOrWhiteSpace text) then
                create MessageMenuItem.Copy "复制消息"
                |> (if not isUser && isLastAssistant then withHint "Ctrl+Shift+C" else id)
              if isUser then
                create MessageMenuItem.EditAndFork "编辑并分叉"
              if not isUser && isLastAssistant then
                create MessageMenuItem.Regenerate "重新生成"
                |> withHint "Ctrl+Shift+G"
              match commitIdOpt, linkSink with
              | Some commitId, Some _ ->
                create (MessageMenuItem.CopyLink commitId) "复制消息链接"
              | _ -> ()
              match commitIdOpt with
              | Some commitId ->
                create (MessageMenuItem.Delete commitId) "删除这条消息"
                |> asDanger
              | None -> () ]

    let private tag (x: MessageMenuItem) = (Microsoft.FSharp.Reflection.FSharpValue.GetUnionFields(x, typeof<MessageMenuItem>) |> fst).Tag
    let entries s u l t c h = old s u l t c h |> List.map _.item
    let label item = (old false false false "x" (Some 1UL) true @ old false true false "x" (Some 1UL) true @ old false false true "x" (Some 1UL) true) |> List.find (fun e -> tag e.item = tag item) |> _.label
    let hint (isUser: bool) (isLastAssistant: bool) item =
        old false isUser isLastAssistant "x" (Some 1UL) true |> List.tryFind (fun e -> tag e.item = tag item) |> Option.map _.hint |> Option.defaultValue ""
    let isDanger item = (old false false false "x" (Some 1UL) true) |> List.tryFind (fun e -> tag e.item = tag item) |> Option.map _.danger |> Option.defaultValue false
