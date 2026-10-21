module Tests

open Xunit
open Wanxiang.App

[<Fact>]
let ``同一个键只执行一次，源码只在需要时取`` () =
    let h = JintScriptHost()
    let mutable fetched = 0

    let src () =
        fetched <- fetched + 1
        "var n = (typeof n === 'undefined' ? 0 : n) + 1;"

    h.LoadOnce("a", src)
    h.LoadOnce("a", src)
    Assert.Equal(1, fetched)
    Assert.True(h.IsLoaded "a")
    Assert.Equal("1", h.EvaluateString("String(n)", []))

[<Fact>]
let ``绑定的值原样进入脚本，引号不需要转义`` () =
    let h = JintScriptHost()
    let tricky = "a'b\"c`${x}\\n"
    Assert.Equal(tricky, h.EvaluateString("__v", [ "__v", box tricky ]))
    Assert.Equal("true", h.EvaluateString("String(__b)", [ "__b", box true ]))

[<Fact>]
let ``console 被垫平，调用不抛`` () =
    let h = JintScriptHost()
    Assert.Equal("ok", h.EvaluateString("console.warn('x'); console.log(1); 'ok'", []))

[<Fact>]
let ``脚本异常原样抛给调用方，之后仍可用`` () =
    let h = JintScriptHost()

    Assert.ThrowsAny<exn>(fun () -> h.EvaluateString("throw new Error('boom')", []) |> ignore)
    |> ignore

    Assert.ThrowsAny<exn>(fun () -> h.LoadOnce("bad", fun () -> "syntax error ("))
    |> ignore

    Assert.False(h.IsLoaded "bad")
    Assert.Equal("2", h.EvaluateString("String(1+1)", []))

[<Fact>]
let ``无限递归被递归上限拦下并抛出`` () =
    // 上限 10000 层；默认 1MB 线程栈装不下这么深的解释器调用，所以在大栈线程上验证上限本身。
    let mutable caught: exn = null

    let t =
        System.Threading.Thread(
            (fun () ->
                let h = JintScriptHost()

                try
                    h.EvaluateString("function f(){return f()}; f()", []) |> ignore
                with e ->
                    caught <- e),
            256 * 1024 * 1024
        )

    t.Start()
    t.Join()
    Assert.NotNull caught
