module SidebarEmptyRender
open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    for q in SidebarEmptySamples.queries do
      for ve in SidebarEmptySamples.bools do
        for hs in SidebarEmptySamples.bools do
          for c in SidebarEmptySamples.bools do
            for l in SidebarEmptySamples.bools do
              for cm in SidebarEmptySamples.bools do
                let v = SidebarEmpty.decide q ve hs c l cm
                sb.Append(sprintf "%A %b %b %b %b %b -> %b %b %A %b %b %A %A" q ve hs c l cm v.searchEmpty v.hostVisible v.kind v.actionVisible v.skeletonVisible v.title v.note).Append('\n') |> ignore
    sb.ToString()
