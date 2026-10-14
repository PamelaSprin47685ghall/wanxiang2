namespace Wanxiang.UI

open System
open System.Collections.Generic

/// 一次增量更新的安排：面板保留前 KeepControls 个控件、其余卸下；
/// Restore 是仍挂着的原末块控件，宿主恢复它们的原外边距；Render 是要依次渲染并挂上的块
type BlockPlan<'B, 'C> = { KeepControls: int; Restore: 'C list; Render: 'B list }

/// 旧 Controls/MarkdownRenderer.fs StreamingMarkdown（720-756）的记账：上一次的原文、
/// 已挂的块与各块占用的控件。解析 Markdown、渲染块、改面板与边距仍归宿主；
/// 宿主先 Accept 原文，再按解析结果 Diff，渲染 Render 里的每块后调 Mounted 登记。
type BlockDiff<'B, 'C when 'B: equality>() =
    let mounted = List<'B * 'C list>()
    let mutable lastRaw: string = null

    /// 当前挂载的块数
    member _.BlockCount = mounted.Count

    /// 原文与上次相同（同一实例或同值）就返回 false，宿主什么都不用做
    member _.Accept(raw: string) : bool =
        if Object.ReferenceEquals(raw, lastRaw) || raw = lastRaw then false
        else
            lastRaw <- raw
            true

    /// 与已挂的块比出共同前缀，丢掉前缀之后的登记，给出安排
    member _.Diff(blocks: 'B list) : BlockPlan<'B, 'C> =
        let wanted = Array.ofList blocks
        let mutable common = 0
        while common < mounted.Count && common < wanted.Length && fst mounted[common] = wanted[common] do
            common <- common + 1
        let keep = mounted |> Seq.take common |> Seq.sumBy (fun (_, cs) -> List.length cs)
        mounted.RemoveRange(common, mounted.Count - common)
        let restore = if mounted.Count > 0 then snd mounted[mounted.Count - 1] else []
        { KeepControls = keep; Restore = restore; Render = List.ofArray wanted[common..] }

    /// 登记刚渲染并挂上的一块及其控件（按 Render 的次序调）
    member _.Mounted(block: 'B, controls: 'C list) = mounted.Add((block, controls))
