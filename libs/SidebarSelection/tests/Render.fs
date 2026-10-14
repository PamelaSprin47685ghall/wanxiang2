module Render

open System.Text
open Wanxiang.UI

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let s = SidebarSelection()
    let id (n: string) = match n.[n.Length - 1] with 'a' -> Samples.a | 'b' -> Samples.b | 'c' -> Samples.c | _ -> Samples.d
    for name, vis in Samples.steps do
        match name with
        | n when n.StartsWith "enter" -> s.Enter(id n)
        | n when n.StartsWith "toggle" -> s.Toggle(id n)
        | "allvis" -> s.ToggleAllVisible vis
        | "exit" -> s.Exit()
        | _ -> ()
        let ch = s.Chrome(vis, Samples.allPinned)
        sb.Append(sprintf "%s %d -> %b %d %A %A | %A" name vis.Length s.Active s.Count (s.SelectedIds vis |> List.map (fun g -> g.ToString().[7])) (s.IsSelected Samples.a) ch).Append('\n') |> ignore
    sb.ToString()
