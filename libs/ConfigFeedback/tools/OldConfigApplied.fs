namespace Wanxiang.UI

open System

// 原样摘自旧仓库 extract-ledger a5a1afc 的 src/Wanxiang.UI/AppShell.fs HandleEvent `| ConfigApplied d ->` 分支；
// configFeedback.Resolve 的结果换成参数 tracked（登记的成功文案），toast / send CatalogRequest / onCompleted 换成记下结果。
[<RequireQualifiedAccess>]
type ConfigAppliedNotice =
    | Success of text: string
    | Failure of text: string
    | Silent

type private Tone = | Success | Failure

module ConfigApplied =
    let notice (tracked: string option) (ok: bool) (errors: string list) : ConfigAppliedNotice * bool =
        let d = {| ok = ok; errors = Array.ofList errors |}
        let mutable shown = ConfigAppliedNotice.Silent
        let mutable catalog = false
        let toast text tone =
            shown <- (match tone with Success -> ConfigAppliedNotice.Success text | Failure -> ConfigAppliedNotice.Failure text)
        let send () = catalog <- true
        let CatalogRequest = ()
        match tracked |> Option.map (fun m -> {| successMessage = m; onCompleted = ignore<bool> |}) with
        | Some feedback ->
            feedback.onCompleted d.ok
            if d.ok then
                if not (String.IsNullOrEmpty feedback.successMessage) then
                    toast feedback.successMessage Success
                send CatalogRequest
            else
                toast (String.Join("；", d.errors)) Failure
        | None ->
            if d.ok then
                toast "配置已保存" Success
                send CatalogRequest
            else
                toast (String.Join("；", d.errors)) Failure
        shown, catalog
