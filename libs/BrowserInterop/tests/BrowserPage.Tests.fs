module BrowserPageTests

open System.Reflection
open System.Runtime.InteropServices.JavaScript
open Xunit
open Wanxiang.Interop

/// 与 wwwroot/main.js 的契约：每个公开方法绑定的 JS 函数名都来自旧 BrowserBridge，模块名都是 "wanxiang"。
let private imports (t: System.Type) =
    [ for m in t.GetMethods(BindingFlags.Public ||| BindingFlags.Static) do
        // 只读元数据：浏览器之外实例化 JSImportAttribute 会抛 PlatformNotSupportedException
        for a in m.GetCustomAttributesData() do
            if a.AttributeType = typeof<JSImportAttribute> then
                yield m.Name, string a.ConstructorArguments[0].Value, string a.ConstructorArguments[1].Value ] |> List.sort

[<Fact>]
let ``JS 绑定名与旧 BrowserBridge 一致`` () =
    let expected = [ "ConversationSucceeded", "wxConversationSucceeded", "wanxiang"; "ResumeEpoch", "wxResumeEpoch", "wanxiang"; "Url", "pageUrl", "wanxiang"; "ViewportWidth", "wxViewportWidth", "wanxiang" ] |> List.sort
    Assert.Equal<(string * string * string) list>(expected, imports typeof<BrowserPage>)

[<Fact>]
let ``NotifyHidden 是导出给 main.js 的入口，触发 Hidden`` () =
    let m = typeof<BrowserPage>.GetMethod("NotifyHidden")
    Assert.Contains(m.GetCustomAttributesData(), fun a -> a.AttributeType = typeof<JSExportAttribute>)
    let mutable n = 0
    let h = System.Action(fun () -> n <- n + 1)
    BrowserPage.add_Hidden h
    BrowserPage.NotifyHidden()
    BrowserPage.remove_Hidden h
    BrowserPage.NotifyHidden()
    Assert.Equal(1, n)

[<Fact>]
let ``没有订阅者时 NotifyHidden 不抛`` () =
    // 「不抛」这件事本身没法直接断言，所以断言它**没有破坏状态**：
    // 没有订阅者时调 NotifyHidden，之后新加的订阅者仍能被正常触发。
    // （旧写法只有 `BrowserPage.NotifyHidden()` 一句：跑完什么也没说。）
    // 第一步：零订阅者时调用（不抛，且不误触发任何处理器）。
    let mutable called = 0
    BrowserPage.NotifyHidden()
    Assert.Equal(0, called)
    // 第二步：状态没被破坏——新加的订阅者仍能被触发。
    let h = System.Action(fun () -> called <- called + 1)
    BrowserPage.add_Hidden h
    BrowserPage.NotifyHidden()
    BrowserPage.remove_Hidden h
    Assert.Equal(1, called)
