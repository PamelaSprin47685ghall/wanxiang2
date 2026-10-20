namespace Wanxiang2.FieldCheck

open System
open System.Runtime.CompilerServices
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Avalonia.Layout
open Avalonia.Markup.Xaml.MarkupExtensions

/// 动态资源绑定辅助：外观值全部由主题管理者的 ResourceDictionary 声明式提供，
/// 本库不写字面样式值，也不做手工订阅。
module private Dyn =
    let res (key: string) =
        let ext = DynamicResourceExtension key
        let sp = { new IServiceProvider with member _.GetService _ = null }
        ext.ProvideValue sp

/// 字段级校验反馈（迁自旧 Controls/Primitives.fs 的 Ui.fieldValidationMessage / setFieldError / clearFieldError /
/// bindValidationSummaryReset / applyValidationFeedback，以及 Interaction.setInvalid / isInvalid）。
/// 每个输入框一条错误行，由本对象创建并持有；用户真的改了文字才清掉错误。
/// 输入壳是输入框的父级 Border（与旧 Ui.textField 一致）。不改边框粗细与内边距，出错时没有横向漂移。
/// 外观走资源键：错误行 = Palette.Danger / Metric.FontCaption / Metric.CaptionLineHeight / Margin.FieldError；
/// 输入壳描边错误时绑 Palette.Danger、复位时绑 Palette.Border（与输入壳的常态描边同源）。
type FieldCheck() =
    let messages = ConditionalWeakTable<TextBox, TextBlock>()

    static let invalidProperty =
        AvaloniaProperty.RegisterAttached<FieldCheck, Border, bool>("IsInvalid", false)

    /// 主题样式表用的类名。
    static member InvalidClass = "invalid"

    static member IsInvalid(shell: Border) = shell.GetValue invalidProperty

    member private _.SetInvalid(shell: Border, invalid: bool) =
        shell.SetValue(invalidProperty, invalid) |> ignore
        if invalid then shell.Classes.Add FieldCheck.InvalidClass |> ignore
        else shell.Classes.Remove FieldCheck.InvalidClass |> ignore
        let key = if invalid then "Palette.Danger" else "Palette.Border"
        shell.Bind(Border.BorderBrushProperty, Dyn.res key) |> ignore

    /// 取得（第一次时创建）这个输入框的错误行。调用方把它放进对应字段组。
    member this.MessageFor(box: TextBox) : TextBlock =
        match messages.TryGetValue box with
        | true, message -> message
        | _ ->
            let message = TextBlock(Text = "", TextWrapping = TextWrapping.Wrap, IsVisible = false)
            message.Bind(TextBlock.ForegroundProperty, Dyn.res "Palette.Danger") |> ignore
            message.Bind(TextBlock.FontSizeProperty, Dyn.res "Metric.FontCaption") |> ignore
            message.Bind(TextBlock.LineHeightProperty, Dyn.res "Metric.CaptionLineHeight") |> ignore
            message.Bind(Layoutable.MarginProperty, Dyn.res "Margin.FieldError") |> ignore
            messages.Add(box, message)
            // 用户开始修正（文本真的变了）才清错误：聚焦切换时 Avalonia 会重放同一文本并抛 TextChanged，
            // 不比较旧值就会把刚写好的提示自己清掉。
            let mutable lastText = box.Text
            box.PropertyChanged.Add(fun args ->
                if args.Property = TextBox.TextProperty then
                    let current = box.Text
                    let changed = current <> lastText
                    lastText <- current
                    if changed && message.IsVisible then
                        message.Text <- ""
                        message.IsVisible <- false
                        AutomationProperties.SetHelpText(box, "")
                        match box.Parent with
                        | :? Border as shell -> this.SetInvalid(shell, false)
                        | _ -> ())
            message

    member this.ApplyError(box: TextBox, message: string) =
        let line = this.MessageFor box
        line.Text <- message
        line.IsVisible <- not (String.IsNullOrWhiteSpace message)
        match box.Parent with
        | :? Border as shell -> this.SetInvalid(shell, true)
        | _ -> ()
        AutomationProperties.SetHelpText(box, message)

    member this.Clear(box: TextBox) =
        let line = this.MessageFor box
        line.Text <- ""
        line.IsVisible <- false
        match box.Parent with
        | :? Border as shell -> this.SetInvalid(shell, false)
        | _ -> ()
        AutomationProperties.SetHelpText(box, "")

    member _.HasError(box: TextBox) =
        match messages.TryGetValue box with
        | true, message -> message.IsVisible
        | _ -> false

    /// 表单级错误摘要：用户动手修正任一字段，摘要立刻收起并清空。
    static member BindSummaryReset(summary: TextBlock option, boxes: TextBox seq) =
        match summary with
        | None -> ()
        | Some region ->
            for box in boxes do
                box.GetObservable(TextBox.TextProperty).Subscribe(fun (_: string) ->
                    if region.IsVisible then
                        region.IsVisible <- false
                        region.Text <- ""
                        AutomationProperties.SetName(region, ""))
                |> ignore

    /// 提交后的统一反馈。errors 按字段顺序给出。
    /// 多错且有摘要：填摘要并聚焦摘要；单错（或没有摘要位）：聚焦首个失败字段；没有错：收起摘要。
    /// 返回应弹出的提示条文字（没有为 None）；提示条由组合者交给提示层。
    static member Apply(errors: (TextBox * string) list, summary: TextBlock option) : string option =
        let focusField (box: TextBox) =
            box.BringIntoView()
            box.Focus(NavigationMethod.Directional) |> ignore
        let single (box: TextBox, message: string) =
            focusField box
            if String.IsNullOrWhiteSpace message then None else Some message
        match errors with
        | [] ->
            summary |> Option.iter (fun region -> region.IsVisible <- false)
            None
        | first :: _ when List.length errors > 1 ->
            match summary with
            | Some region ->
                let joined = String.Join("；", errors |> List.map snd)
                region.Text <- sprintf "有 %d 处需要修正：%s" (List.length errors) joined
                region.IsVisible <- true
                AutomationProperties.SetName(region, region.Text)
                region.BringIntoView()
                region.Focus(NavigationMethod.Directional) |> ignore
                Some(sprintf "有 %d 处需要修正，请查看表单顶部摘要。" (List.length errors))
            | None -> single first
        | first :: _ ->
            summary |> Option.iter (fun region -> region.IsVisible <- false)
            single first
