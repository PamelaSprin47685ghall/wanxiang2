namespace Wanxiang2.FoldHeader

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Controls.Documents
open Avalonia.Controls.Primitives
open Avalonia.Input
open Avalonia.Layout
open Avalonia.VisualTree
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

/// 折叠披露头（MessageCard 里思考过程 / 工具活动 / 错误细节三处共用的头部）：
/// 基于 Avalonia 原生 Expander 封装，动画由 Avalonia 伪类/Transitions 声明式接管，
/// 标题文本由使用方往 Caption 里加 Run，展开收起触发 Toggled。
type FoldHeader(expanded: bool) as this =
    inherit Expander()

    let toggled = Event<bool>()
    let mutable reportState = true

    let caption =
        TextBlock(
            FontWeight = FontWeight.Medium,
            FontSize = 12.0,
            LineHeight = 16.0,
            VerticalAlignment = VerticalAlignment.Center
        )

    let row =
        StackPanel(Orientation = Orientation.Horizontal, Spacing = 8.0, VerticalAlignment = VerticalAlignment.Center)

    let mutable headerBtn: Button = null

    let updateStatus () =
        let target: Control =
            if not (isNull headerBtn) then
                headerBtn :> Control
            else
                this :> Control

        if reportState then
            AutomationProperties.SetItemStatus(target, (if this.IsExpanded then "已展开" else "已折叠"))
        else
            AutomationProperties.SetItemStatus(target, null)

    do
        this.IsExpanded <- expanded
        this.Classes.Add "fold-header"

        caption.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.TextMuted")
        |> ignore

        row.Children.Add caption
        this.Header <- row

        this.PropertyChanged.Add(fun e ->
            if e.Property = Expander.IsExpandedProperty then
                let isExp = e.NewValue :?> bool
                updateStatus ()
                toggled.Trigger isExp)

        updateStatus ()

    /// 可交互的头部控件（兼容旧接口，使用方可挂外边距或自动化属性）
    member this.HeaderControl: Control =
        match
            this.GetVisualDescendants()
            |> Seq.tryPick (function
                | :? ToggleButton as t when t.Name = "ExpanderHeader" -> Some(t :> Control)
                | _ -> None)
        with
        | Some btn -> btn
        | None -> this :> Control

    /// 旧接口兼容：Button 类型的 Header（提供薄包装以满足特定使用方的命名与按键）
    member this.Header: Button =
        if isNull headerBtn then
            let btn =
                Button(
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Focusable = true,
                    Margin = Thickness 0.0,
                    Padding = Thickness(4.0, 4.0),
                    CornerRadius = CornerRadius 4.0,
                    BorderThickness = Thickness 1.0,
                    Content = this
                )

            btn.Classes.Add "ghost"
            btn.Classes.Add "hover-bordered"
            btn.Click.Add(fun _ -> this.Toggle())
            headerBtn <- btn
            updateStatus ()

        headerBtn

    /// 标题文本块：使用方往 Inlines 里加 Run
    member _.Caption = caption
    /// 头部内容行：[前置件]、标题、[尾随件]
    member _.Row = row
    member this.Expanded = this.IsExpanded

    /// 是否经 ItemStatus 报展开状态（旧工具活动头不报，默认报）
    member this.ReportState
        with get () = reportState
        and set (v: bool) =
            reportState <- v
            updateStatus ()

    /// 开合之后触发，参数是新的展开状态
    member _.Toggled = toggled.Publish

    /// 在标题之前插一个件（旧工具活动头的状态图标）
    member _.AddLeading(c: Control) =
        let idx = row.Children.IndexOf caption
        row.Children.Insert(max 0 idx, c)

    /// 在标题之后加一个件（旧思考过程头的复制按钮）
    member _.AddTrailing(c: Control) = row.Children.Add c
    /// 按一下切换展开/折叠
    member this.Toggle() = this.IsExpanded <- not this.IsExpanded

    /// 单入口：从 FoldHeaderVS 渲染
    member this.Render(vs: FoldHeaderVS) =
        caption.Text <- vs.Title

        if vs.Expanded <> this.IsExpanded then
            this.IsExpanded <- vs.Expanded

/// 标题里常用的一段 Run
module FoldHeader =
    let run (text: string) (brush: IBrush) = Run(text, Foreground = brush)
