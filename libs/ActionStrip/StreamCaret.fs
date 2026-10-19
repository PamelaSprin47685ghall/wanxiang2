namespace Wanxiang2.StreamCaret

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions
open Avalonia.Media
open Wanxiang2.WorkspaceState

module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

[<RequireQualifiedAccess>]
/// 流式光标：采用声明式 anim-breathe 动效类，单入口 Render(StreamCaretVS)。
type StreamCaret(?breath: unit -> float) =
    let caret =
        Border(
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = Thickness 0.0,
            Width = 7.0,
            Height = 15.0,
            CornerRadius = CornerRadius 1.5)
    let mutable attached = false
    let mutable reduced = false
    let mutable peak = 1.0
    let breathFn = defaultArg breath (fun () -> 1.0)
    let refresh () =
        if reduced || not attached then
            caret.Classes.Remove "anim-breathe" |> ignore
            caret.Opacity <- peak
        else
            caret.Opacity <- breathFn ()
            if not (caret.Classes.Contains "anim-breathe") then caret.Classes.Add "anim-breathe"
    do
        caret.Bind(Border.BackgroundProperty, Dyn.res "Palette.Accent") |> ignore
        caret.Classes.Add "anim-breathe"
        caret.AttachedToVisualTree.Add(fun _ ->
            attached <- true
            refresh ())
        caret.DetachedFromVisualTree.Add(fun _ ->
            attached <- false
            caret.Classes.Remove "anim-breathe" |> ignore)

    member _.Host: Control = caret
    member _.IsBreathing = caret.Classes.Contains "anim-breathe" && not reduced
    /// 走一帧（测试不等真计时器）
    member _.Step() = if attached && not reduced then caret.Opacity <- breathFn ()

    /// 单入口：Render(StreamCaretVS)
    member _.Render(vs: StreamCaretVS) =
        caret.IsVisible <- vs.Visible
        if vs.Visible then refresh () else caret.Classes.Remove "anim-breathe" |> ignore

    /// 兼容重载：Render(visible)
    member this.Render(visible: bool) =
        this.Render({ Visible = visible })
