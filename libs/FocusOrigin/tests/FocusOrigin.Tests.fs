module Wanxiang2.FocusOrigin.Tests.Tests

open Avalonia.Controls
open Avalonia.Input
open Avalonia.Threading
open Xunit
open Wanxiang2.FocusOrigin

[<Fact>]
let ``键盘与程序化聚焦挂类名，指针聚焦与失焦去掉`` () =
    Headless.run (fun () ->
        let a = Button(Content = "a")
        let b = Button(Content = "b")
        let panel = StackPanel()
        panel.Children.Add a
        panel.Children.Add b
        FocusOrigin.report a
        let w = Window(Content = panel)
        w.Show()
        a.Focus NavigationMethod.Tab |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.Contains(FocusOrigin.ProgrammaticClass, a.Classes)
        b.Focus NavigationMethod.Tab |> ignore
        Dispatcher.UIThread.RunJobs()
        Assert.DoesNotContain(FocusOrigin.ProgrammaticClass, a.Classes)
        a.Focus NavigationMethod.Unspecified |> ignore
        Assert.Contains(FocusOrigin.ProgrammaticClass, a.Classes)
        b.Focus NavigationMethod.Tab |> ignore
        a.Focus NavigationMethod.Pointer |> ignore
        Assert.DoesNotContain(FocusOrigin.ProgrammaticClass, a.Classes)
        w.Close())
