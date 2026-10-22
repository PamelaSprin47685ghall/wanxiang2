namespace Wanxiang.UI.Tests

open System
open System.Threading
open Avalonia
open Avalonia.Headless
open Avalonia.Themes.Fluent

// 与其他组合者的测试台同一形状：不用 Avalonia.Headless.XUnit（.NET 10 上会死锁），
// 直接用 HeadlessUnitTestSession。字体相关的用例需要真实的 Skia 字体后端
// （`IFontManagerImpl` 由 Skia 平台注册），所以这里 `UseSkia`。
// 测试台用**真的** Wanxiang.UI.App（而不是只加 FluentTheme 的空壳）：
// 弹出层的内嵌字体是应用级样式（App.Initialize 里装的），只有真 App 才会装。
type TestApp() =
    inherit Wanxiang.UI.App()

type TestAppBuilder =
    static member BuildAvaloniaApp() =
        AppBuilder
            .Configure<TestApp>()
            .UseSkia()
            .UseHeadless(AvaloniaHeadlessPlatformOptions(UseHeadlessDrawing = false))

module Headless =
    // 隔离级别用 PerAssembly 而不是 PerTest：这里的 TestApp 是**真的** Wanxiang.UI.App，
    // 它的 Initialize 会装进程级单例（SharedTheme.styles 的 Styles 组、
    // SharedTheme.manager.Resources 的资源字典）。Avalonia 的资源提供者只有一个 owner，
    // PerTest 下每个用例新建一个 Application，第二个用例再挂同一份单例就抛
    // "The Styles already has a owner."
    // PerAssembly 让整个测试程序集只有一个 Application，等同生产的一次启动：
    // 单例资源只装一次，和真实宿主是同一条路。
    let private session =
        lazy (HeadlessUnitTestSession.StartNew(typeof<TestAppBuilder>, AvaloniaTestIsolationLevel.PerAssembly))

    /// 在 Avalonia UI 线程上同步执行测试体。
    let run (body: unit -> unit) : unit =
        session.Value.Dispatch(Action body, CancellationToken.None).GetAwaiter().GetResult()
