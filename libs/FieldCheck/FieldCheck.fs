namespace Wanxiang2.FieldCheck

open System
open System.Runtime.CompilerServices
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading

/// 字段校验的外观。全部由主题管理者提供活的实例；本库不写字面样式值。
type FieldCheckLook =
    { /// 错误行文字
      ErrorForeground: IBrush
      /// 校验错误时输入壳的描边
      InvalidStroke: IBrush
      /// 正常时输入壳的描边
      NormalStroke: IBrush
      MessageFontSize: IObservable<float>
      MessageLineHeight: IObservable<float>
      /// 错误行相对字段的边距
      MessageMargin: IObservable<Thickness> }

/// 字段级校验反馈（迁自旧 Controls/Primitives.fs 的 Ui.fieldValidationMessage / setFieldError / clearFieldError /
/// bindValidationSummaryReset / applyValidationFeedback，以及 Interaction.setInvalid / isInvalid）。
/// 每个输入框一条错误行，由本对象创建并持有；用户真的改了文字才清掉错误。
/// 输入壳是输入框的父级 Border（与旧 Ui.textField 一致）。不改边框粗细与内边距，出错时没有横向漂移。
type FieldCheck(look: FieldCheckLook) =
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
        shell.BorderBrush <- (if invalid then look.InvalidStroke else look.NormalStroke)

    /// 取得（第一次时创建）这个输入框的错误行。调用方把它放进对应字段组。
    member this.MessageFor(box: TextBox) : TextBlock =
        match messages.TryGetValue box with
        | true, message -> message
        | _ ->
            let message = TextBlock(Text = "", Foreground = look.ErrorForeground, TextWrapping = TextWrapping.Wrap, IsVisible = false)
            message.Bind(TextBlock.FontSizeProperty, look.MessageFontSize) |> ignore
            message.Bind(TextBlock.LineHeightProperty, look.MessageLineHeight) |> ignore
            message.Bind(Avalonia.Layout.Layoutable.MarginProperty, look.MessageMargin) |> ignore
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

    member this.SetError(box: TextBox, message: string) =
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
            Dispatcher.UIThread.Post(fun () ->
                if box.IsEffectivelyVisible && box.IsEnabled then
                    box.BringIntoView()
                    box.Focus(NavigationMethod.Directional) |> ignore)
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
                Dispatcher.UIThread.Post(fun () ->
                    if region.IsEffectivelyVisible then
                        region.BringIntoView()
                        region.Focus(NavigationMethod.Directional) |> ignore)
                Some(sprintf "有 %d 处需要修正，请查看表单顶部摘要。" (List.length errors))
            | None -> single first
        | first :: _ ->
            summary |> Option.iter (fun region -> region.IsVisible <- false)
            single first
