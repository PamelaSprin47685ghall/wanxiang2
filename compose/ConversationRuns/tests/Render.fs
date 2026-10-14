/// 金标准渲染：同一份源码分别对旧代码摘录（tools/OldGenerationOutcome.fs）与本组合者编译。
module Render

open Wanxiang.Core
open Wanxiang.UI

let all () =
    let given = GenerationError.create GenerationErrorKind.ProviderRateLimited "限流了"
    [ for status in [ "completed"; "cancelled"; "failed"; "exploded"; "" ] do
          for err in [ None; Some given ] do
              let e = GenerationOutcome.errorOf status err
              for active in [ true; false ] ->
                  sprintf "%s|%b|%b => %A ; %A" status err.IsSome active (e |> Option.map (fun x -> x.kind, x.message, x.detail)) (GenerationOutcome.notice status e active) ]
    |> List.map (fun l -> l + "\n")
    |> String.concat ""
