namespace Wanxiang.UI

open Wanxiang.Core

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs HandleEvent `| GenerationFinished d ->` 分支：
// `let error = ...` 与 toast 的 match；toast 换成返回 FinishNotice，activeConvId = Some d.conversationId 换成 isActive。
[<RequireQualifiedAccess>]
type FinishNotice =
    | Stopped
    | FailureToast of text: string
    | Quiet

module GenerationOutcome =
    let errorOf (status: string) (error: GenerationError option) : GenerationError option =
        let d = {| status = status; error = error |}
        let error =
            match d.status with
            | "completed" | "cancelled" -> None
            | "failed" ->
                match d.error with
                | Some _ as error -> error
                | None ->
                    Some(GenerationError.create GenerationErrorKind.UnknownFailure "生成失败，主机没有说明原因。可以重试；反复出现时请在设置里「检查主机」。")
            | unknown ->
                Some(
                    GenerationError.create GenerationErrorKind.UnknownFailure "生成结束状态未知，已按失败处理。"
                    |> GenerationError.withDetail (sprintf "未知状态：%s" unknown))
        error

    let notice (status: string) (error: GenerationError option) (isActive: bool) : FinishNotice =
        let d = {| status = status |}
        let mutable r = FinishNotice.Quiet
        let toast text _ = r <- (if text = "已停止" then FinishNotice.Stopped else FinishNotice.FailureToast text)
        match d.status, error with
        | "cancelled", _ when isActive -> toast "已停止" ()
        | _, Some err when not isActive ->
            toast (GenerationError.display err) ()
        | _ -> ()
        r
