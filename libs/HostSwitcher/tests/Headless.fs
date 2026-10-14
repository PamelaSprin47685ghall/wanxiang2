namespace Wanxiang2.HostSwitcher.Tests

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
        AppBuilder.Configure<TestApp>().UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = true))

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
