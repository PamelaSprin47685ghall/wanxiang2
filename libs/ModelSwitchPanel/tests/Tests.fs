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
        | :? Panel as panel ->
            for child in panel.Children do
                yield! descendants child
        | :? ContentControl as host ->
            match host.Content with
            | :? Control as child -> yield! descendants child
            | _ -> ()
        | :? Decorator as d when not (isNull d.Child) -> yield! descendants d.Child
        | _ -> ()
    }

let appliesNote = "从下一条回复开始生效"

/// 组合者的筛选与分组：子串筛选；无查询且多组时显示组头（ModelPickerList.groups 的规则）
let slots =
    {
        Groups =
            fun q opts ->
                let shown =
                    opts
                    |> List.filter (fun o ->
                        String.IsNullOrWhiteSpace q || o.Model.Contains q || o.ProviderLabel.Contains q)

                let grouped = shown |> List.groupBy _.ProviderLabel
                let headers = String.IsNullOrWhiteSpace q && grouped.Length > 1

                grouped
                |> List.map (fun (l, items) -> (if headers then Some l else None), items)
        SearchHelp = "输入筛选，回车选第一条，下方向键进入列表，Esc 关闭"
        EmptyText = "没有匹配的模型。"
        AppliesNote = appliesNote
    }

let options =
    [
        {
            ProviderId = "oa"
            ProviderLabel = "OpenAI"
            Model = "gpt-4o"
            IsCurrent = true
            IsDefault = false
            Probe = ProbeState.Probed 3
        }
        {
            ProviderId = "oa"
            ProviderLabel = "OpenAI"
            Model = "o3"
            IsCurrent = false
            IsDefault = false
            Probe = ProbeState.Unknown
        }
        {
            ProviderId = "ds"
            ProviderLabel = "DeepSeek"
            Model = "deepseek-chat"
            IsCurrent = false
            IsDefault = true
            Probe = ProbeState.Failed
        }
    ]

let rowNames (root: Control) =
    descendants root
    |> Seq.choose (function
        | :? Border as b when b.Focusable ->
            Some(AutomationProperties.GetName b + "，" + AutomationProperties.GetHelpText b)
        | _ -> None)
    |> List.ofSeq

/// 组合者的激活：当前项不切换
let picker (onPick: ModelChoice -> unit) =
    fun (o: ModelChoice) ->
        if not o.IsCurrent then
            onPick o

let enter (search: TextBox) =
    search.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, Source = search))

// 迁自旧 ModelChipUiTests。
[<Fact>]
let ``switcher shows provider, default and current, plus the next reply note`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build None slots options "切换本对话的模型" ignore
        let names = rowNames root
        Assert.Equal(3, names.Length)
        Assert.Contains("gpt-4o，OpenAI，当前", names)
        Assert.Contains("deepseek-chat，DeepSeek，默认", names)

        let texts =
            descendants root
            |> Seq.choose (function
                | :? TextBlock as t -> Some t.Text
                | _ -> None)
            |> List.ofSeq

        Assert.Contains(appliesNote, texts)
        Assert.Contains("默认", texts)
        Assert.Contains("已探测 3 个", texts)
        Assert.Contains("探测失败", texts)
        Assert.Contains("OpenAI", texts))

/// 探针三态是**互斥语义**，色点是这层语义的可读出口：已探明配 Success、
/// 失败配 Danger、未知不画点也不出标记文字。
///
/// 此前只有文字断言：把两个分支都改成 `Palette.Danger`，整组用例仍全绿——
/// 「状态点有颜色语义」只是外观巧合，没有任何守护。
///
/// **断言必须落在「色点实际用哪个画笔」这一步。** 只断一个纯函数式的
/// 「状态 → 键」决策表、或只断「有没有 6x6 的点」，都绕得过真正的缺陷
/// （`probeDot` 不读入参、恒用一个颜色）——那一环才是要守的东西。
/// 色点用 `DynamicResource` 取值，所以在应用资源里播种两个画笔，再比较实例。
[<Fact>]
let ``probe dot draws the semantic colour of each known state`` () =
    Headless.run (fun () ->
        let success = SolidColorBrush(Colors.Green)
        let danger = SolidColorBrush(Colors.Red)
        let res = Application.Current.Resources
        res["Palette.Success"] <- box success
        res["Palette.Danger"] <- box danger

        let root = ModelSwitchPanel.build None slots options "t" ignore

        // `DynamicResource` 只在挂进已显示窗口的可视树后求值；不 Show 的话背景是 null。
        let win = Window(Width = 400.0, Height = 600.0, Content = root)

        try
            win.Show()
            win.UpdateLayout()
            Dispatcher.UIThread.RunJobs()

            /// 色点靠样式类认，不靠「6x6 且无子节点」这种尺寸巧合。
            let dotOf (row: Border) =
                descendants row
                |> Seq.tryPick (function
                    | :? Border as b when b.Classes.Contains ModelSwitchPanel.probeDotClass -> Some b
                    | _ -> None)

            /// 按模型名找到那一行（读屏名就是模型名）。
            let rowOf (model: string) =
                descendants root
                |> Seq.pick (function
                    | :? Border as b when b.Focusable && AutomationProperties.GetName b = model -> Some b
                    | _ -> None)

            // 三条选项：已探明（gpt-4o）、未知（o3）、失败（deepseek-chat）。未知不画点。
            Assert.Null(dotOf (rowOf "o3"))

            // 关键：**逐行绑定**。「已探明」那行必须是 Success 画笔，「失败」那行必须是 Danger。
            // 只断言「两个画笔都出现过」是假绿——把两个键对调，集合不变，用例仍绿，
            // 而界面上已探明会显示红点、失败显示绿点。
            let probedDot =
                dotOf (rowOf "gpt-4o") |> Option.defaultWith (fun () -> failwith "已探明行没有色点")

            let failedDot =
                dotOf (rowOf "deepseek-chat")
                |> Option.defaultWith (fun () -> failwith "失败行没有色点")

            Assert.True(obj.ReferenceEquals(probedDot.Background, success), "已探明行该拿 Success 画笔")
            Assert.True(obj.ReferenceEquals(failedDot.Background, danger), "失败行该拿 Danger 画笔")
        finally
            win.Close())

/// 行高读 `Metric.MenuItemMinHeight`，不写常数。
///
/// 原先这里硬编码 34，而该档的值恰好也是 34——「两处数值相同、来源不同」，
/// 改刻度时界面不会跟。同类缺陷在这个仓已经出现过三次（字号、思考块折叠、
/// 代码折行），每次都表现为「改偏好/改刻度没反应」。
///
/// 断言方式：把资源调成两个不同的值，读行宿主的实际高度。写死常数时第二次必红。
[<Fact>]
let ``row height follows the menu item metric, not a constant`` () =
    Headless.run (fun () ->
        let rowOf (root: Control) (model: string) =
            descendants root
            |> Seq.pick (function
                | :? Border as b when b.Focusable && AutomationProperties.GetName b = model -> Some b
                | _ -> None)

        let heightWith (metric: float) =
            let res = Application.Current.Resources
            res["Metric.MenuItemMinHeight"] <- box metric

            let root = ModelSwitchPanel.build None slots options "t" ignore
            let win = Window(Width = 400.0, Height = 600.0, Content = root)

            try
                win.Show()
                win.UpdateLayout()
                Dispatcher.UIThread.RunJobs()
                (rowOf root "gpt-4o").MinHeight
            finally
                win.Close()

        Assert.Equal(44.0, heightWith 44.0)
        Assert.Equal(52.0, heightWith 52.0))

/// 「未知不画点」与上一条分工：上一条管配哪个颜色，这条管画不画。
/// 只在 `Unknown` 分支塞一个色点时，只有这条红。
[<Fact>]
let ``unknown probe state draws no status marker at all`` () =
    Headless.run (fun () ->
        let onlyUnknown =
            [
                {
                    ProviderId = "oa"
                    ProviderLabel = "OpenAI"
                    Model = "o3"
                    IsCurrent = false
                    IsDefault = false
                    Probe = ProbeState.Unknown
                }
            ]

        let root = ModelSwitchPanel.build None slots onlyUnknown "t" ignore

        // 与第一条同口径：挂进已显示窗口再读控件树。
        let win = Window(Width = 400.0, Height = 600.0, Content = root)

        try
            win.Show()
            win.UpdateLayout()
            Dispatcher.UIThread.RunJobs()

            let texts =
                descendants root
                |> Seq.choose (function
                    | :? TextBlock as t -> Some t.Text
                    | _ -> None)
                |> List.ofSeq

            // 断「没有任何探针标记文案」，而不是硬编码某一个计数文本：
            // 未知态若冒出第三种写法（如「未探测」），这里也要红。
            let markerWords = [ "已探测"; "探测失败"; "未探测" ]

            for w in markerWords do
                Assert.DoesNotContain(texts, fun t -> t.Contains w)

            let dots =
                descendants root
                |> Seq.filter (function
                    | :? Border as b when b.Classes.Contains ModelSwitchPanel.probeDotClass -> true
                    | _ -> false)
                |> List.ofSeq

            Assert.Empty dots
        finally
            win.Close())

[<Fact>]
let ``typing filters rows and enter picks the first hit`` () =
    Headless.run (fun () ->
        let mutable picked: ModelChoice option = None

        let root =
            ModelSwitchPanel.build None slots options "t" (picker (fun o -> picked <- Some o))

        let search =
            descendants root
            |> Seq.pick (function
                | :? TextBox as t -> Some t
                | _ -> None)

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

        let root =
            ModelSwitchPanel.build None slots options "t" (fun o ->
                activated <- activated + 1
                picker (fun _ -> picked <- picked + 1) o)

        let search =
            descendants root
            |> Seq.pick (function
                | :? TextBox as t -> Some t
                | _ -> None)

        search.Text <- "gpt"
        Dispatcher.UIThread.RunJobs()
        enter search
        Assert.Equal(1, activated)
        Assert.Equal(0, picked))

// 迁自旧 A11yRegressionTests `模型切换器写明键盘路径且空结果会播报`（旧测试查源码；这里查控件属性）。
[<Fact>]
let ``模型切换器写明键盘路径且空结果会播报`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build None slots options "t" ignore

        let search =
            descendants root
            |> Seq.pick (function
                | :? TextBox as t -> Some t
                | _ -> None)

        Assert.Equal(slots.SearchHelp, AutomationProperties.GetHelpText search)

        let empty =
            descendants root
            |> Seq.pick (function
                | :? TextBlock as t when t.Text = slots.EmptyText -> Some t
                | _ -> None)

        Assert.Equal(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting empty)
        Assert.False empty.IsVisible
        search.Text <- "zzz"
        Dispatcher.UIThread.RunJobs()
        Assert.True empty.IsVisible)

[<Fact>]
let ``down from search enters the list and up from the top returns`` () =
    Headless.run (fun () ->
        let root = ModelSwitchPanel.build None slots options "t" ignore
        let window = Window(Width = 400.0, Height = 500.0, Content = root)
        window.Show()
        Dispatcher.UIThread.RunJobs()

        let search =
            descendants root
            |> Seq.pick (function
                | :? TextBox as t -> Some t
                | _ -> None)

        let rows =
            descendants root
            |> Seq.choose (function
                | :? Border as b when b.Classes.Contains ModelSwitchPanel.rowClass -> Some b
                | _ -> None)
            |> List.ofSeq
        // 无查询且两组：显示组头
        Assert.Equal(
            2,
            descendants root
            |> Seq.filter (fun c -> c.Classes.Contains "section-label")
            |> Seq.length
        )

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
