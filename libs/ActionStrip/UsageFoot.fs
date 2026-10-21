namespace Wanxiang2.UsageFoot

open System
open System.Globalization
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key

        let sp =
            { new IServiceProvider with
                member _.GetService _ = null
            }

        ext.ProvideValue sp

/// 一次生成的用量（字段对应旧 GenerationUsage）
type UsageFigures =
    {
        Prompt: int option
        Completion: int option
        Total: int option
        DurationMs: int64 option
        Cached: int option
        Detail: string
    }

/// 时间戳：脚注里的简短时刻与悬停 / 读屏的完整时间
type StampText = { Short: string; Full: string }

/// 消息脚注里的时间与用量（旧 MessageCard.renderUsage + footer）：标签用最淡档、数字用等宽中档，
/// 合成一行，悬停时数字提亮。
module UsageFoot =
    let private n (v: int) =
        v.ToString("N0", CultureInfo.InvariantCulture)

    /// 合计：上游没给时由输入 + 输出补出
    let total (f: UsageFigures) =
        match f.Total, f.Prompt, f.Completion with
        | Some t, _, _ -> Some t
        | None, Some p, Some c -> Some(p + c)
        | _ -> None

    /// 读屏名
    let speech (f: UsageFigures) =
        let s = Option.map n >> Option.defaultValue "-"
        sprintf "Token 使用情况：输入 %s，输出 %s，总计 %s" (s f.Prompt) (s f.Completion) (s (total f))

    /// 用量一行；没有任何 token 与耗时信息时为 None
    let usage (f: UsageFigures) : Control option =
        let t = total f

        if t.IsNone && f.Prompt.IsNone && f.Completion.IsNone && f.DurationMs.IsNone then
            None
        else
            let tb = TextBlock(VerticalAlignment = VerticalAlignment.Center, FontSize = 11.0)
            let numbers = ResizeArray<Run>()

            let number (text: string) =
                let r = Run(text)
                r.Bind(Run.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore
                r.Bind(Run.FontFamilyProperty, Dyn.res "FontFamily.Mono") |> ignore
                numbers.Add r
                tb.Inlines.Add r

            let sep () =
                let r = Run(" · ")
                r.Bind(Run.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
                tb.Inlines.Add r

            let metric (label: string) (v: int) =
                let r = Run(label + " ")
                r.Bind(Run.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
                tb.Inlines.Add r
                number (n v)

            let mutable hasPrev = false

            match f.Prompt, f.Completion with
            | Some p, Some c ->
                metric "入" p
                sep ()
                metric "出" c
                hasPrev <- true

                match t with
                | Some total when total <> p + c ->
                    sep ()
                    metric "总" total
                | _ -> ()
            | Some p, None ->
                metric "入" p
                hasPrev <- true
            | None, Some c ->
                metric "出" c
                hasPrev <- true
            | None, None ->
                match t with
                | Some total ->
                    metric "tokens" total
                    hasPrev <- true
                | None -> ()

            match f.DurationMs with
            | Some ms ->
                if hasPrev then
                    sep ()

                number (
                    if ms >= 1000L then
                        sprintf "%.1fs" (float ms / 1000.0)
                    else
                        sprintf "%dms" ms
                )
            | None -> ()

            match f.Cached with
            | Some cached ->
                if hasPrev then
                    sep ()

                metric "缓存" cached
            | None -> ()

            tb.PointerEntered.Add(fun _ ->
                for r in numbers do
                    r.Bind(Run.ForegroundProperty, Dyn.res "Palette.Text") |> ignore)

            tb.PointerExited.Add(fun _ ->
                for r in numbers do
                    r.Bind(Run.ForegroundProperty, Dyn.res "Palette.TextMuted") |> ignore)

            AutomationProperties.SetName(tb, speech f)
            ToolTip.SetTip(tb, f.Detail)
            Some(tb :> Control)

    /// 整条脚注：时间（可选）+ 用量（可选）；都没有时为 None。alignRight 用于用户消息
    let line (stamp: StampText option) (figures: UsageFigures option) (alignRight: bool) : Control option =
        let small (text: string) =
            let t =
                TextBlock(Text = text, FontSize = 11.0, VerticalAlignment = VerticalAlignment.Center)

            t.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextFaint") |> ignore
            t

        let timeBlock (s: StampText) =
            let t = small s.Short
            ToolTip.SetTip(t, s.Full)
            AutomationProperties.SetName(t, s.Full)
            t

        match stamp, figures |> Option.bind usage with
        | None, None -> None
        | Some s, None ->
            let t = timeBlock s

            t.HorizontalAlignment <-
                (if alignRight then
                     HorizontalAlignment.Right
                 else
                     HorizontalAlignment.Left)

            Some(t :> Control)
        | None, Some u -> Some u
        | Some s, Some u ->
            let panel =
                StackPanel(
                    Orientation = Orientation.Horizontal,
                    Spacing = 4.0,
                    VerticalAlignment = VerticalAlignment.Center
                )

            panel.Children.Add(timeBlock s)
            panel.Children.Add(small " · ")
            panel.Children.Add u
            Some(panel :> Control)

    /// 单入口：从 UsageFootVS 渲染
    let Render (vs: UsageFootVS, alignRight: bool) : Control option =
        let stamp =
            match vs.Timestamp with
            | Some short ->
                Some
                    {
                        Short = short
                        Full = defaultArg vs.TimestampFull short
                    }
            | None -> None

        let figures =
            {
                Prompt = vs.PromptTokens
                Completion = vs.CompletionTokens
                Total = vs.TotalTokens
                DurationMs = vs.DurationMs
                Cached = vs.CachedTokens
                Detail = vs.Detail
            }

        line stamp (Some figures) alignRight
