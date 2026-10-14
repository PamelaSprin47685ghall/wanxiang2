namespace Wanxiang2.UsageFoot

open System
open System.Globalization
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Layout
open Avalonia.Media

[<RequireQualifiedAccess>]
type FootInk =
    | Text
    | TextMuted
    | TextFaint

[<RequireQualifiedAccess>]
type FootGauge =
    /// 旧 fontMicro
    | Font
    /// 时间与用量之间（旧 spaceXs）
    | Gap

type UsageFootLook =
    { Ink: FootInk -> IBrush
      Gauge: FootGauge -> IObservable<float>
      MonoFont: IObservable<FontFamily> }

/// 一次生成的用量（字段对应旧 GenerationUsage）
type UsageFigures =
    { Prompt: int option
      Completion: int option
      Total: int option
      DurationMs: int64 option
      Cached: int option
      /// 悬停的完整明细（旧 GenerationUsage.formatDetail）
      Detail: string }

/// 时间戳：脚注里的简短时刻与悬停 / 读屏的完整时间
type StampText = { Short: string; Full: string }

/// 消息脚注里的时间与用量（旧 MessageCard.renderUsage + footer）：标签用最淡档、数字用等宽中档，
/// 合成一行，悬停时数字提亮。
module UsageFoot =
    let private n (v: int) = v.ToString("N0", CultureInfo.InvariantCulture)

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
    let usage (look: UsageFootLook) (f: UsageFigures) : Control option =
        let t = total f
        if t.IsNone && f.Prompt.IsNone && f.Completion.IsNone && f.DurationMs.IsNone then None
        else
            let muted = look.Ink FootInk.TextMuted
            let faint = look.Ink FootInk.TextFaint
            let tb = TextBlock(VerticalAlignment = VerticalAlignment.Center)
            Follow.live tb (look.Gauge FootGauge.Font) (fun s -> tb.FontSize <- s)
            let numbers = ResizeArray<Run>()
            let number (text: string) =
                let r = Run(text, Foreground = muted)
                Follow.live tb look.MonoFont (fun fam -> r.FontFamily <- fam)
                numbers.Add r
                tb.Inlines.Add r
            let sep () = tb.Inlines.Add(Run(" · ", Foreground = faint))
            let metric (label: string) (v: int) =
                tb.Inlines.Add(Run(label + " ", Foreground = faint))
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
                if hasPrev then sep ()
                number (if ms >= 1000L then sprintf "%.1fs" (float ms / 1000.0) else sprintf "%dms" ms)
            | None -> ()
            // 缓存命中收尾：命中省钱，用户看得到才可能去开提示缓存
            match f.Cached with
            | Some cached ->
                if hasPrev then sep ()
                metric "缓存" cached
            | None -> ()
            let text = look.Ink FootInk.Text
            tb.PointerEntered.Add(fun _ -> for r in numbers do r.Foreground <- text)
            tb.PointerExited.Add(fun _ -> for r in numbers do r.Foreground <- muted)
            AutomationProperties.SetName(tb, speech f)
            ToolTip.SetTip(tb, f.Detail)
            Some(tb :> Control)

    /// 整条脚注：时间（可选）+ 用量（可选）；都没有时为 None。alignRight 用于用户消息
    let line (look: UsageFootLook) (stamp: StampText option) (figures: UsageFigures option) (alignRight: bool) : Control option =
        let faint = look.Ink FootInk.TextFaint
        let small (text: string) =
            let t = TextBlock(Text = text, Foreground = faint, VerticalAlignment = VerticalAlignment.Center)
            Follow.live t (look.Gauge FootGauge.Font) (fun s -> t.FontSize <- s)
            t
        let timeBlock (s: StampText) =
            let t = small s.Short
            // 简短时刻悬停补完整时间戳，读屏取同一份完整文本
            ToolTip.SetTip(t, s.Full)
            AutomationProperties.SetName(t, s.Full)
            t
        match stamp, figures |> Option.bind (usage look) with
        | None, None -> None
        | Some s, None ->
            let t = timeBlock s
            t.HorizontalAlignment <- (if alignRight then HorizontalAlignment.Right else HorizontalAlignment.Left)
            Some(t :> Control)
        | None, Some u -> Some u
        | Some s, Some u ->
            let panel = StackPanel(Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center)
            Follow.live panel (look.Gauge FootGauge.Gap) (fun g -> panel.Spacing <- g)
            panel.Children.Add(timeBlock s)
            panel.Children.Add(small " · ")
            panel.Children.Add u
            Some(panel :> Control)
