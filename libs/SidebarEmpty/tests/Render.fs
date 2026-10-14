module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for q in Samples.queries do
      for ve in Samples.bools do
        for hs in Samples.bools do
          for c in Samples.bools do
            for l in Samples.bools do
              for cm in Samples.bools do
                let v = SidebarEmpty.decide q ve hs c l cm
                sb.Append(sprintf "%A %b %b %b %b %b -> %b %b %A %b %b %A %A" q ve hs c l cm v.searchEmpty v.hostVisible v.kind v.actionVisible v.skeletonVisible v.title v.note).Append('\n') |> ignore
    sb.ToString()
