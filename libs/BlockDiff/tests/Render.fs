module Render

open System
open System.Text
open Wanxiang.UI

/// 宿主替身：控件 = (名字, 原底边距, 当前底边距)；一块渲染成 1 个控件，代码块渲染成 2 个（头 + 体）
type Ctl = { name: string; original: int; mutable bottom: int }

let parse (raw: string) =
    raw.Split([| "\n\n" |], StringSplitOptions.RemoveEmptyEntries) |> List.ofArray

/// 金样文本：生成器（旧代码）与测试（新库）共用同一份渲染
let render () =
    let sb = StringBuilder()
    let d = BlockDiff<string, Ctl>()
    let panel = Collections.Generic.List<Ctl>()
    let mutable last = null
    let mutable made = 0
    for r in Samples.raws do
        let raw = if r = "!same" then last else String(r.ToCharArray())
        last <- raw
        let accepted = d.Accept raw
        if accepted then
            let plan = d.Diff(parse raw)
            if panel.Count > plan.KeepControls then panel.RemoveRange(plan.KeepControls, panel.Count - plan.KeepControls)
            for c in plan.Restore do c.bottom <- c.original
            for block in plan.Render do
                let n = if block.StartsWith "```" then 2 else 1
                let controls = [ for i in 1 .. n -> made <- made + 1; { name = sprintf "c%d" made; original = 8 * i; bottom = 8 * i } ]
                d.Mounted(block, controls)
                panel.AddRange controls
            if panel.Count > 0 then panel[panel.Count - 1].bottom <- 0
        let shown = panel |> Seq.map (fun c -> sprintf "%s:%d" c.name c.bottom) |> String.concat " "
        sb.Append(sprintf "%A accepted=%b blocks=%d panel=[%s]" raw accepted d.BlockCount shown).Append('\n') |> ignore
    sb.ToString()
