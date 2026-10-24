namespace Wanxiang2.Theme.Tests

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

// Avalonia 平台状态与 HeadlessUnitTestSession 是进程级全局量，多模块并行会并发调用 EnsureIsolatedApplication 互踩。
[<assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)>]
do ()
