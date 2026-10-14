namespace Wanxiang.UI

open System
open System.Collections.Generic

type BlockPlan<'B, 'C> = { KeepControls: int; Restore: 'C list; Render: 'B list }

// 照抄旧 Controls/MarkdownRenderer.fs StreamingMarkdown.Update 的记账：
// Update 拆在 Accept（原文比较）与 Diff（前缀、卸载、恢复末块）两处，Markdown.commonPrefix 照抄成本地循环。
type BlockDiff<'B, 'C when 'B: equality>() =
    let mounted = Collections.Generic.List<'B * 'C list>()
    let mutable lastRaw: string = null
    let commonPrefix (mounted: 'B list) (blocks: 'B list) : int =
        let mounted = Array.ofList mounted
        let blocks = Array.ofList blocks
        let mutable common = 0
        while common < mounted.Length && common < blocks.Length && mounted[common] = blocks[common] do
            common <- common + 1
        common
    member _.BlockCount = mounted.Count
    member _.Accept(raw: string) : bool =
        if not (Object.ReferenceEquals(raw, lastRaw) || raw = lastRaw) then
            lastRaw <- raw
            true
        else false
    member _.Diff(blocksList: 'B list) : BlockPlan<'B, 'C> =
        let blocks = blocksList |> Array.ofList
        let common = commonPrefix (mounted |> Seq.map fst |> List.ofSeq) (List.ofArray blocks)
        let keepControls = mounted |> Seq.take common |> Seq.sumBy (fun (_, cs) -> List.length cs)
        mounted.RemoveRange(common, mounted.Count - common)
        let restore = if mounted.Count > 0 then snd mounted[mounted.Count - 1] else []
        { KeepControls = keepControls; Restore = restore; Render = [ for index in common .. blocks.Length - 1 -> blocks[index] ] }
    member _.Mounted(block: 'B, controls: 'C list) = mounted.Add((block, controls))
