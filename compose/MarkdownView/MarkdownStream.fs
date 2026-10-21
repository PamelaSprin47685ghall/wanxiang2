namespace Wanxiang2.MarkdownView

open Avalonia
open Avalonia.Controls
open Wanxiang.UI

/// 流式回复的增量 Markdown 面板（旧 StreamingMarkdown，决策 292）。
/// 已写完的块不会再变，只替换第一个变了的块之后的控件；前面的文本块不重建、不重新挂树。
/// 结果与 MarkdownView.RenderText 同构：同一纵向面板、同一末块边距规则。
type MarkdownStream(view: MarkdownView) =
    let panel = StackPanel(Orientation = Layout.Orientation.Vertical, Spacing = 0.0)
    /// 已挂的块与各自控件（含原始外边距：末块底边距会被临时清零），上次原文归 BlockDiff
    let diff = BlockDiff<MdBlock, Control * Thickness>()

    member _.Panel: Control = panel :> Control

    /// 当前挂载的块数
    member _.BlockCount = diff.BlockCount

    member _.Update(raw: string) =
        if diff.Accept raw then
            let plan = diff.Diff(Markdown.parse raw)

            if panel.Children.Count > plan.KeepControls then
                panel.Children.RemoveRange(plan.KeepControls, panel.Children.Count - plan.KeepControls)

            for control, margin in plan.Restore do
                control.Margin <- margin

            for block in plan.Render do
                let controls = view.RenderBlock block
                diff.Mounted(block, controls |> List.map (fun c -> c, c.Margin))

                for control in controls do
                    panel.Children.Add control

            if panel.Children.Count > 0 then
                let last = panel.Children[panel.Children.Count - 1]
                let m = last.Margin
                last.Margin <- Thickness(m.Left, m.Top, m.Right, 0.0)
