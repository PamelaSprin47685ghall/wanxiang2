namespace Wanxiang.UI

// 照抄旧 Views/SettingsProviders.fs 的 describe 与 ApplyProbe 分支；控件与 toast 调用换成记录字段
open System

[<RequireQualifiedAccess>]
type VisionVerdict = | Yes | No | Unknown
[<RequireQualifiedAccess>]
type VisionWhy = | User | Provider | Name
[<RequireQualifiedAccess>]
type ProbeTone = | Info | Warning | Failure

type ProbeFeedback = { editorStatus: string option; fillModels: bool; cacheModels: bool; toast: string; tone: ProbeTone }

module ProviderProbeText =
    let visionLine (m: string) (support: VisionVerdict) (source: VisionWhy) =
        let verdict =
            match support with
            | VisionVerdict.Yes -> "能看图"
            | VisionVerdict.No -> "只吃文字"
            | VisionVerdict.Unknown -> "未知（放行，由模型服务决定）"
        let why =
            match source with
            | VisionWhy.User -> "你的指定"
            | VisionWhy.Provider -> "模型服务提供"
            | VisionWhy.Name -> "按名字推断"
        sprintf "%s：%s（%s）" m verdict why

    let visionSummary (models: string list) (judge: string -> VisionVerdict * VisionWhy) =
        let describe m = let s, w = judge m in visionLine m s w
        models |> List.truncate 12 |> List.map describe |> String.concat "\n"

    let feedback (editorOpen: bool) (ok: bool) (count: int) (errMsg: string) =
        let mutable r = { editorStatus = None; fillModels = false; cacheModels = false; toast = ""; tone = ProbeTone.Info }
        let setStatus s = r <- { r with editorStatus = Some s }
        let toast t tone = r <- { r with toast = t; tone = tone }
        let models = List.replicate count "m"
        if editorOpen then
            if ok && not (List.isEmpty models) then
                setStatus ""
                r <- { r with fillModels = true }
                toast (sprintf "已获取 %d 个模型，已填入草稿，请确认后保存。" (List.length models)) ProbeTone.Info
            elif ok then
                setStatus "模型服务返回了空模型列表，模型框未改动。"
                toast "模型服务返回了空模型列表。" ProbeTone.Warning
            else
                setStatus errMsg
                toast errMsg ProbeTone.Failure
        else
            if ok then
                r <- { r with cacheModels = true }
                if not (List.isEmpty models) then
                    toast (sprintf "已获取 %d 个模型。可在编辑模型服务中确认后保存。" (List.length models)) ProbeTone.Info
                else
                    toast "模型服务返回了空模型列表。" ProbeTone.Warning
            else
                toast errMsg ProbeTone.Failure
        r

    let unknownReason = "未知原因"
