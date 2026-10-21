module Render

open System.Text
open Wanxiang.Client
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新组合）共用同一份渲染
let render () =
    let sb = StringBuilder()

    for i in Samples.issues do
        let retry = ConnectionIssue.retry i |> Option.map ConnectIssueText.retryLabel

        sb.Append(sprintf "%A -> %s | retry %A" i (ConnectIssueText.describe i) retry).Append('\n')
        |> ignore

    sb.ToString()
