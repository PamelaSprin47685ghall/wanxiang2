module OldIdleChip

open Wanxiang.UI

// 原样摘自旧 extract-ledger a5a1afc src/Wanxiang.UI/AppShell.fs Render 的空闲分支（模型标签部分）；
// composer.SetModelLabel / SetModelVision 换成记录调用。私有成员无法从旧程序集调用，故用摘录做差分。
let run (modelLabel: string option) (draftModelSelection: (string * string) option) (catalog: Catalog) : string list =
    let calls = ResizeArray<string>()
    let composer =
        {| SetModelLabel = fun (l: string) -> calls.Add("label " + l)
           SetModelVision = fun (v: VisionSupport, d: string) -> calls.Add(sprintf "vision %A %s" v d) |}
    match modelLabel with
    | Some label -> composer.SetModelLabel label
    | None ->
            match draftModelSelection |> Option.orElse (Catalog.defaultSelection catalog) with
            | Some(providerId, model) ->
                composer.SetModelLabel(Catalog.describeModel providerId model catalog)
                composer.SetModelVision(fst (Catalog.visionOf providerId model catalog), Catalog.describeModel providerId model catalog)
            | None ->
                composer.SetModelLabel "未选择模型"
                composer.SetModelVision(VisionUnknown, "")
    List.ofSeq calls
