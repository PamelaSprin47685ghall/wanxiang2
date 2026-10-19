namespace Wanxiang2.WorkspaceState

type RevealTarget =
    /// 指定消息（搜索命中、分叉里被编辑的那条）
    | RevealMessage of uint64
    /// “某条消息的下一条”（切回原文版本：原对话里分叉点之后的那一条；None = 第一条）
    | RevealAfter of uint64 option

module RevealTarget =

    /// 在对话已加载的消息 commitId（升序）里解析出具体目标；None = 目标不在已加载窗口。
    let resolve (target: RevealTarget) (loadedCommits: uint64 list) : uint64 option =
        match target with
        | RevealMessage c -> if List.contains c loadedCommits then Some c else None
        | RevealAfter None -> List.tryHead loadedCommits
        | RevealAfter(Some prev) ->
            match List.tryFindIndex ((=) prev) loadedCommits with
            | Some i when i + 1 < List.length loadedCommits -> Some(List.item (i + 1) loadedCommits)
            | _ -> None
