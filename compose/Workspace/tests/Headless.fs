namespace Wanxiang2.WorkspaceTests

open System
open System.Threading
open Avalonia
open Avalonia.Headless
open Avalonia.Themes.Fluent

// 与旧仓库相同：不用 Avalonia.Headless.XUnit（.NET 10 上会死锁），直接用 HeadlessUnitTestSession。
type TestApp() =
    inherit Application()
    override this.Initialize() = this.Styles.Add(FluentTheme())

type TestAppBuilder =
    static member BuildAvaloniaApp() =
        AppBuilder.Configure<TestApp>().UseSkia().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

module Headless =
    let private session =
        lazy (HeadlessUnitTestSession.StartNew(typeof<TestAppBuilder>, AvaloniaTestIsolationLevel.PerTest))

    /// 在 Avalonia UI 线程上同步执行测试体。
    let run (body: unit -> unit) : unit =
        session.Value.Dispatch(Action body, CancellationToken.None).GetAwaiter().GetResult()

/// 测试用的活尺寸：模拟主题管理者改值。
type Cell<'T>(initial: 'T) =
    let mutable value = initial
    let subs = Collections.Generic.List<IObserver<'T>>()
    member _.Set v =
        value <- v
        for s in List.ofSeq subs do s.OnNext v
    interface IObservable<'T> with
        member _.Subscribe o =
            subs.Add o
            o.OnNext value
            { new IDisposable with member _.Dispose() = subs.Remove o |> ignore }

module Desk =
    open Avalonia.Controls
    open Avalonia.Media
    open Avalonia.Threading
    open Avalonia.VisualTree
    open Avalonia.Automation
    open Avalonia.Input
    open Wanxiang2.OverlayDesk

    /// 测试用的浮层外观（与 OverlayDesk 自己的测试同值）。
    let look () : OverlayDeskMetrics =
        { Layer =
            { Scrim = SolidColorBrush(Color.FromArgb(80uy, 0uy, 0uy, 0uy))
              Surface = Brushes.White
              Edge = Brushes.Gray
              EdgeWidth = Cell(Thickness 1.0)
              DialogRadius = Cell(CornerRadius 8.0)
              PopupRadius = Cell(CornerRadius 6.0)
              DialogPadding = Cell(Thickness 16.0)
              PopupPadding = Cell(Thickness 2.0)
              DialogShadow = Cell(BoxShadows())
              PopupShadow = Cell(BoxShadows())
              ViewportInset = Cell 12.0
              PopupGap = Cell 4.0
              PopupMinWidth = Cell 180.0
              ScrimFade = Cell TimeSpan.Zero
              ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Toasts =
            { Surface = Brushes.White
              Edge = Brushes.Gray
              Text = Brushes.Black
              Accent = (fun _ -> Brushes.Gray)
              AccentWidth = Cell 2.0
              AccentRadius = Cell(CornerRadius 999.0)
              AccentOpacity = Cell 0.6
              EdgeWidth = Cell(Thickness 1.0)
              Radius = Cell(CornerRadius 8.0)
              Padding = Cell(Thickness(16.0, 12.0))
              Shadow = Cell(BoxShadows())
              FontSize = Cell 12.0
              LineHeight = Cell 18.0
              ColumnSpacing = Cell 12.0
              Spacing = Cell 8.0
              Margin = Cell(Thickness(24.0, 0.0, 24.0, 32.0))
              MaxWidth = Cell 480.0
              BodyMaxHeight = Cell 160.0
              // 减弱动效：主题给 0
              Fade = Cell TimeSpan.Zero
              FadeEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Menu =
            { Text = Brushes.Black
              Muted = Brushes.Gray
              Faint = Brushes.LightGray
              Danger = Brushes.Red
              Accent = Brushes.Blue
              Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
              Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
              FontSize = Cell 12.0
              HintFontSize = Cell 10.0
              IconSlotWidth = Cell 16.0
              IconHeight = Cell 16.0
              RightSlotMinWidth = Cell 16.0
              HintMargin = Cell(Thickness(12.0, 0.0, 0.0, 0.0))
              ColumnSpacing = Cell 6.0
              ItemPadding = Cell(Thickness(8.0, 4.0))
              ItemRadius = Cell(CornerRadius 4.0)
              ItemMinHeight = Cell 32.0
              ItemSpacing = Cell 2.0
              MaxHeight = Cell 200.0
              DividerMargin = Cell(Thickness(6.0, 2.0))
              HeaderMargin = Cell(Thickness(8.0, 6.0, 8.0, 2.0))
              SelectPadding = Cell(Thickness(8.0, 4.0))
              SelectRadius = Cell(CornerRadius 8.0)
              SelectEdgeWidth = Cell(Thickness 1.0)
              SelectCaptionMaxWidth = Cell 260.0 }
          MenuMinWidth = Cell 200.0
          GroupedMenuMinWidth = Cell 240.0
          DwellFailureMs = Cell 9000.0
          DwellWarningMs = Cell 6500.0
          DwellDefaultMs = Cell 4000.0
          DismissTick = TimeSpan.FromMilliseconds 100.0 }

    let show (root: Control) (w: float) (h: float) =
        let window = Window(Width = w, Height = h, Content = root)
        window.Show()
        Dispatcher.UIThread.RunJobs()
        window

    let named (root: Avalonia.Visual) (name: string) =
        root.GetVisualDescendants() |> Seq.choose (function :? Control as c when AutomationProperties.GetName c = name -> Some c | _ -> None) |> Seq.tryHead

    let texts (root: Avalonia.Visual) =
        root.GetVisualDescendants() |> Seq.choose (function :? TextBlock as t -> Some t.Text | _ -> None) |> List.ofSeq

    let key (c: Control) (k: Key) =
        c.RaiseEvent(KeyEventArgs(RoutedEvent = InputElement.KeyDownEvent, Key = k, KeyModifiers = KeyModifiers.None, Source = c))
        Dispatcher.UIThread.RunJobs()
