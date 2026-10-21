namespace Wanxiang2.MarkdownView

open Avalonia.Controls
open Avalonia.Input
open Avalonia.Media

/// 可点的原生交互件，继承 Button 获得原生自动化、键盘导航与悬停反馈。
type ActionSpot() as this =
    inherit Button()

    do
        this.Focusable <- true
        this.Background <- Brushes.Transparent

    override this.OnKeyDown(e: KeyEventArgs) =
        base.OnKeyDown(e)

        if not e.Handled && this.IsEnabled && e.Key = Key.Space then
            e.Handled <- true
            this.RaiseEvent(Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent, this))

    /// 点击走原生 Click 事件
    member this.OnInvoke(f: unit -> unit) =
        this.Click.Add(fun _ ->
            if this.IsEnabled then
                f ())
