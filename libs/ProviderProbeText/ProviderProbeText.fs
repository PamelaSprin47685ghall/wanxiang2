namespace Wanxiang.UI

open System

/// 看图判断（与 ModelVision 同形，本库不引用它；限定访问）
[<RequireQualifiedAccess>]
type VisionVerdict =
    | Yes
    | No
    | Unknown

/// 看图判断的来源（限定访问）
[<RequireQualifiedAccess>]
type VisionWhy =
    | User
    | Provider
    | Name

/// 提示语气（限定访问）
[<RequireQualifiedAccess>]
type ProbeTone =
    | Info
    | Warning
    | Failure

/// 探活回执的反馈：editorStatus 只在编辑器打开且是当前服务时给（Some "" 表示清空）
type ProbeFeedback =
    { editorStatus: string option
      /// 是否把模型填进草稿
      fillModels: bool
      /// 编辑器没开时缓存模型列表
      cacheModels: bool
      toast: string
      tone: ProbeTone }

/// 旧 Views/SettingsProviders.fs 的看图说明（visionAutoText）与探活回执文案（ApplyProbe）
module ProviderProbeText =

    let visionLine (model: string) (verdict: VisionVerdict) (why: VisionWhy) =
        let v =
            match verdict with
            | VisionVerdict.Yes -> "能看图"
            | VisionVerdict.No -> "只吃文字"
            | VisionVerdict.Unknown -> "未知（放行，由模型服务决定）"
        let w =
            match why with
            | VisionWhy.User -> "你的指定"
            | VisionWhy.Provider -> "模型服务提供"
            | VisionWhy.Name -> "按名字推断"
        sprintf "%s：%s（%s）" model v w

    /// 最多列 12 个模型
    let visionSummary (models: string list) (judge: string -> VisionVerdict * VisionWhy) =
        models |> List.truncate 12 |> List.map (fun m -> let v, w = judge m in visionLine m v w) |> String.concat "\n"

    /// errorMessage 为 ProbeFailureKind.parse 之后的说明（调用方做）
    let feedback (editorOpen: bool) (ok: bool) (modelCount: int) (errorMessage: string) =
        match editorOpen, ok with
        | true, true when modelCount > 0 ->
            { editorStatus = Some ""; fillModels = true; cacheModels = false
              toast = sprintf "已获取 %d 个模型，已填入草稿，请确认后保存。" modelCount; tone = ProbeTone.Info }
        | true, true ->
            { editorStatus = Some "模型服务返回了空模型列表，模型框未改动。"; fillModels = false; cacheModels = false
              toast = "模型服务返回了空模型列表。"; tone = ProbeTone.Warning }
        | true, false ->
            { editorStatus = Some errorMessage; fillModels = false; cacheModels = false; toast = errorMessage; tone = ProbeTone.Failure }
        | false, true when modelCount > 0 ->
            { editorStatus = None; fillModels = false; cacheModels = true
              toast = sprintf "已获取 %d 个模型。可在编辑模型服务中确认后保存。" modelCount; tone = ProbeTone.Info }
        | false, true ->
            { editorStatus = None; fillModels = false; cacheModels = true; toast = "模型服务返回了空模型列表。"; tone = ProbeTone.Warning }
        | false, false ->
            { editorStatus = None; fillModels = false; cacheModels = false; toast = errorMessage; tone = ProbeTone.Failure }

    /// 主机没给原因时交给 ProbeFailureKind.parse 的缺省
    let unknownReason = "未知原因"
