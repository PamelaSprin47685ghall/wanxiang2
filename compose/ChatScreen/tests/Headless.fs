namespace Wanxiang2.ChatScreenTests

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
              DialogShadow = BoxShadows()
              PopupShadow = BoxShadows()
              PopupGap = 4.0
              PopupMinWidth = 180.0
              ScrimFade = TimeSpan.Zero
              ScrimEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Toasts =
            { Surface = Brushes.White
              Edge = Brushes.Gray
              Text = Brushes.Black
              Accent = (fun _ -> Brushes.Gray)
              AccentOpacity = 0.6
              Padding = Thickness(16.0, 12.0)
              Shadow = BoxShadows()
              LineHeight = 18.0
              ColumnSpacing = 12.0
              Spacing = 8.0
              Margin = Thickness(24.0, 0.0, 24.0, 32.0)
              MaxWidth = 480.0
              BodyMaxHeight = 160.0
              // 减弱动效：主题给 0
              Fade = TimeSpan.Zero
              FadeEasing = Avalonia.Animation.Easings.CubicEaseOut() }
          Menu =
            { Text = Brushes.Black
              Muted = Brushes.Gray
              Faint = Brushes.LightGray
              Danger = Brushes.Red
              Accent = Brushes.Blue
              Check = (fun b -> Border(Width = 12.0, Height = 12.0, Background = b))
              Chevron = (fun b -> Border(Width = 10.0, Height = 10.0, Background = b))
              IconSlotWidth = 16.0
              ItemMinHeight = 32.0
              MaxHeight = 200.0 }
          MenuMinWidth = 200.0
          GroupedMenuMinWidth = 240.0
          DwellFailureMs = 9000.0
          DwellWarningMs = 6500.0
          DwellDefaultMs = 4000.0
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
