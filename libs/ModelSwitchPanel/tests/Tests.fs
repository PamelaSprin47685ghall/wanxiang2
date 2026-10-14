module Wanxiang2.ModelSwitchPanel.Tests.Tests

open System
open Avalonia
open Avalonia.Automation
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media
open Avalonia.Threading
open Xunit
open Wanxiang2.ModelSwitchPanel

let rec descendants (control: Control) : seq<Control> =
    seq {
        yield control
        match control with
        | :? Panel as panel -> for child in panel.Children do yield! descendants child
        | :? ContentControl as host ->
            match host.Content with
            | :? Control as child -> yield! descendants child
            | _ -> ()
        | :? Decorator as d when not (isNull d.Child) -> yield! descendants d.Child
        | _ -> ()
    }

let look () =
    { Text = Brushes.Black; Muted = Brushes.Gray; Faint = Brushes.LightGray; Accent = Brushes.Blue
      TagText = Brushes.Gray; TagSurface = Brushes.Beige; FieldSurface = Brushes.White; FieldEdge = Brushes.Gray; Caret = Brushes.Blue
      Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
      BodyFontSize = Cell 14.0; SmallFontSize = Cell 12.0; CaptionFontSize = Cell 12.0; MicroFontSize = Cell 10.5
      TagRadius = Cell(CornerRadius 999.0); TagPadding = Cell(Thickness(6.0, 1.0))
      ItemPadding = Cell(Thickness(12.0, 6.0)); ItemRadius = Cell(CornerRadius 4.0); ItemMinHeight = Cell 32.0
      ItemSpacing = Cell 2.0; ColumnSpacing = Cell 8.0; MarkSpacing = Cell 4.0; StackSpacing = Cell 4.0
      HeaderMargin = Cell(Thickness 4.0); EmptyMargin = Cell(Thickness 4.0); HeadingMargin = Cell(Thickness 4.0)
      NoteMargin = Cell(Thickness 4.0); FieldMargin = Cell(Thickness 4.0); ListMaxHeight = Cell 360.0; PanelWidth = Cell 300.0
      FieldPadding = Cell(Thickness 6.0); FieldRadius = Cell(CornerRadius 8.0); FieldEdgeWidth = Cell(Thickness 1.0); FieldTextMinHeight = Cell 24.0 }

let appliesNote = "从下一条回复开始生效"

/// 组合者的筛选与分组：子串筛选；无查询且多组时显示组头（ModelPickerList.groups 的规则）
let slots =
    { Groups =
        fun q opts ->
            let shown = opts |> List.filter (fun o -> String.IsNullOrWhiteSpace q || o.Model.Contains q || o.ProviderLabel.Contains q)
            let grouped = shown |> List.groupBy _.ProviderLabel
            let headers = String.IsNullOrWhiteSpace q && grouped.Length > 1
            grouped |> List.map (fun (l, items) -> (if headers then Some l else None), items)
      SearchHelp = "输入筛选，回车选第一条，下方向键进入列表，Esc 关闭"
      EmptyText = "没有匹配的模型。"
      AppliesNote = appliesNote }

let options =
    [ { ProviderId = "oa"; ProviderLabel = "OpenAI"; Model = "gpt-4o"; IsCurrent = true; IsDefault = false }
      { ProviderId = "oa"; ProviderLabel = "OpenAI"; Model = "o3"; IsCurrent = false; IsDefault = false }
      { ProviderId = "ds"; ProviderLabel = "DeepSeek"; Model = "deepseek-chat"; IsCurrent = false; IsDefault = true } ]

let rowNames (root: Control) =
    descendants root
    |> Seq.choose (function
        | :? Border as b when b.Focusable -> Some(AutomationProperties.GetName b + "，" + AutomationProperties.GetHelpText b)
        | _ -> None)
    |> List.ofSeq

/// 组合者的激活：当前项不切换
let picker (onPick: ModelChoice -> unit) = fun (o: ModelChoice) -> if not o.IsCurrent then onPick o

let enter (search: TextBox) = search.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = search))

// 迁自旧 ModelChipUiTests。
[<Fact>]
let ``switcher shows provider, default and current, plus the next reply note`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build (look ()) slots options "切换本对话的模型" ignore
        let names = rowNames root
        Assert.Equal(3, names.Length)
        Assert.Contains("gpt-4o，OpenAI，当前", names)
        Assert.Contains("deepseek-chat，DeepSeek，默认", names)
        let texts = descendants root |> Seq.choose (function :? TextBlock as t -> Some t.Text | _ -> None) |> List.ofSeq
        Assert.Contains(appliesNote, texts)
        Assert.Contains("默认", texts)
        Assert.Contains("OpenAI", texts))

[<Fact>]
let ``typing filters rows and enter picks the first hit`` () =
    Headless.run (fun () ->
        let mutable picked: ModelChoice option = None
        let root = ModelSwitchPanel.build (look ()) slots options "t" (picker (fun o -> picked <- Some o))
        let search = descendants root |> Seq.pick (function :? TextBox as t -> Some t | _ -> None)
        search.Text <- "deep"
        Dispatcher.UIThread.RunJobs()
        Assert.Equal<string list>([ "deepseek-chat，DeepSeek，默认" ], rowNames root)
        enter search
        Assert.Equal(Some "deepseek-chat", picked |> Option.map _.Model))

[<Fact>]
let ``picking the current model is a no-op`` () =
    Headless.run (fun () ->
        let mutable picked = 0
        let mutable activated = 0
        let root = ModelSwitchPanel.build (look ()) slots options "t" (fun o -> activated <- activated + 1; picker (fun _ -> picked <- picked + 1) o)
        let search = descendants root |> Seq.pick (function :? TextBox as t -> Some t | _ -> None)
        search.Text <- "gpt"
        Dispatcher.UIThread.RunJobs()
        enter search
        Assert.Equal(1, activated)
        Assert.Equal(0, picked))

// 迁自旧 A11yRegressionTests `模型切换器写明键盘路径且空结果会播报`（旧测试查源码；这里查控件属性）。
[<Fact>]
let ``模型切换器写明键盘路径且空结果会播报`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build (look ()) slots options "t" ignore
        let search = descendants root |> Seq.pick (function :? TextBox as t -> Some t | _ -> None)
        Assert.Equal(slots.SearchHelp, AutomationProperties.GetHelpText search)
        let empty = descendants root |> Seq.pick (function :? TextBlock as t when t.Text = slots.EmptyText -> Some t | _ -> None)
        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting empty)
        Assert.False empty.IsVisible
        search.Text <- "zzz"
        Dispatcher.UIThread.RunJobs()
        Assert.True empty.IsVisible)

[<Fact>]
let ``down from search enters the list and up from the top returns`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build (look ()) slots options "t" ignore
        let window = Window(Width = 400.0, Height = 500.0, Content = root)
        window.Show()
        Dispatcher.UIThread.RunJobs()
        let search = descendants root |> Seq.pick (function :? TextBox as t -> Some t | _ -> None)
        let rows = descendants root |> Seq.choose (function :? Border as b when b.Classes.Contains ModelSwitchPanel.rowClass -> Some b | _ -> None) |> List.ofSeq
        // 无查询且两组：显示组头
        Assert.Equal(2, descendants root |> Seq.filter (fun c -> c.Classes.Contains "section-label") |> Seq.length)
        search.Focus() |> ignore
        search.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down))
        Assert.True rows[0].IsFocused
        rows[0].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down))
        rows[1].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down))
        rows[2].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down))
        Assert.True rows[2].IsFocused
        rows[2].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up))
        rows[1].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up))
        rows[0].RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Up))
        Assert.True search.IsFocused
        Assert.Equal(300.0, root.Width)
        window.Close())
