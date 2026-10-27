namespace Wanxiang2.AppShell.Tests

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
    // 隔离级别用 PerAssembly 而不是 PerTest：PerTest 会在每个用例前重建 Application 与 Dispatcher，
    // 而 Avalonia 12.1.2 的 `EnsureIsolatedApplication` 在前一个用例有后台续体向 Dispatcher 投递时，
    // 新 Application 的 `DefaultRenderLoop.Add` 会落到旧 Dispatcher 的线程上，抛
    // 「The calling thread cannot access this object because a different thread owns it」。
    // 整个程序集只建一次 Application，与生产「一次启动」同形；样式与资源字典只安装到当前用例的根视图，
    // 不往全局 `Application.Current.Styles` 累叠。
    let private session =
        lazy (HeadlessUnitTestSession.StartNew(typeof<TestAppBuilder>, AvaloniaTestIsolationLevel.PerAssembly))

    let private gate = obj ()

    let private locked (body: unit -> unit) : unit = lock gate body

    /// 在 Avalonia UI 线程上同步执行测试体。
    let run (body: unit -> unit) : unit =
        locked (fun () -> session.Value.Dispatch(Action body, CancellationToken.None).GetAwaiter().GetResult())
